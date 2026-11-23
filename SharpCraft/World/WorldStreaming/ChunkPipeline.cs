using SharpCraft.SharpMath;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.World.Meshing;
using SharpCraft.World.Generation;
using SharpCraft.World.Lighting;
using SharpCraft.Diagnostics;
using SharpCraft.Rendering;

using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Diagnostics;

namespace SharpCraft.World.WorldStreaming;

internal class ChunkPipeline : IDisposable, IAsyncDisposable
{
    private const int ChannelCapacity = 512;
    
    private const double TickBudgetMilliseconds = 2.0;
    
    private const int MaxItemsPerPhase = ChannelCapacity * 2;

    private const int MinRelights = 16;
    private const int MinResults = 16;
    private const int MinMarks = 64;
    private const int MinDeletions = 64;
    private const int MinFresh = 16;
    private const int MinFlush = 16;
    
    private const int StrandedScanPerTick = 256;

    private static readonly Vec3<int>[] NeighborOffsets =
    [
        new(-1, 0, 0), new(1, 0, 0),
        new(0, -1, 0), new(0, 1, 0),
        new(0, 0, -1), new(0, 0, 1)
    ];

    private readonly JobProfiler generationProfile = new();
    private readonly JobProfiler lightingProfile = new();
    private readonly JobProfiler meshingProfile = new();
    private long staleResults;
    public double CompletionMilliseconds { get; private set; }
    private ulong NextVersion => Interlocked.Increment(ref field);

    private readonly ChunkVolume volume;
    private readonly ChunkGenerator chunkGenerator;
    private readonly ChunkMesher chunkMesher;
    private readonly MeshSink meshSink;

    private readonly CancellationTokenSource cts = new();

    private readonly ConcurrentDictionary<Vec3<int>, ChunkRecord> registry = [];

    private readonly Queue<Vec3<int>> freshQueue = [];
    private readonly Queue<Vec3<int>> markedForDeletionQueue = [];

    private readonly Queue<WorkItem> pendingGeneration = [];
    private readonly Queue<WorkItem> pendingLighting = [];
    private readonly Queue<WorkItem> pendingMeshing = [];

    private readonly HashSet<Vec3<int>> pendingDeletion = [];
    private readonly Queue<Vec3<int>> deletionQueue = [];
    
    private IEnumerator<KeyValuePair<Vec3<int>, ChunkRecord>>? strandedScan;

    private long tickDeadline;

    private readonly Channel<WorkItem> genChannel = Channel.CreateBounded<WorkItem>(
        new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = false,
            SingleWriter = true
        });

    private readonly Channel<WorkItem> lightChannel = Channel.CreateBounded<WorkItem>(
        new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = false,
            SingleWriter = true
        });

    private readonly Channel<WorkItem> meshChannel = Channel.CreateBounded<WorkItem>(
        new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = false,
            SingleWriter = true
        });

    private readonly Channel<WorkResult> resultChannel = Channel.CreateBounded<WorkResult>(
        new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly Channel<RelightRequest> relightChannel = Channel.CreateBounded<RelightRequest>(
        new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly Task[] genWorkers;
    private readonly Task[] lightingWorkers;
    private readonly Task[] meshingWorkers;

    public ChunkPipeline(ChunkVolume volume, ChunkGenerator chunkGenerator, ChunkMesher chunkMesher, MeshSink meshSink)
    {
        this.volume = volume;
        this.chunkGenerator = chunkGenerator;
        this.chunkMesher = chunkMesher;
        this.meshSink = meshSink;

        CancellationToken ct = cts.Token;

        int computeBudget = Math.Max(1, Environment.ProcessorCount - 2);
        int genWorkersCount = Math.Max(1, computeBudget / 2);
        int lightWorkersCount = Math.Max(1, computeBudget / 4);
        int meshWorkersCount = Math.Max(1, computeBudget - genWorkersCount - lightWorkersCount);

        generationProfile.Workers = genWorkersCount;
        lightingProfile.Workers = lightWorkersCount;
        meshingProfile.Workers = meshWorkersCount;

        genWorkers = Enumerable.Range(0, genWorkersCount).Select(_ => Task.Run(() => GenerateChunkAsync(ct), ct))
            .ToArray();
        lightingWorkers = Enumerable.Range(0, lightWorkersCount).Select(_ => Task.Run(() => LightChunkAsync(ct), ct))
            .ToArray();
        meshingWorkers = Enumerable.Range(0, meshWorkersCount).Select(_ => Task.Run(() => MeshChunkAsync(ct), ct))
            .ToArray();
    }

    public void Schedule(List<Vec3<int>> toGenerate, List<Vec3<int>> toRemove)
    {
        foreach (var id in toRemove)
        {
            markedForDeletionQueue.Enqueue(id);
        }

        foreach (var id in toGenerate)
        {
            freshQueue.Enqueue(id);
        }
    }
    
    public StreamingStatistics GetStatistics() => new(
        pendingGeneration.Count + genChannel.Reader.Count,
        pendingLighting.Count + lightChannel.Reader.Count,
        pendingMeshing.Count + meshChannel.Reader.Count)
    {
        Generation = generationProfile.Snapshot(Oldest(pendingGeneration, genChannel)),
        Lighting = lightingProfile.Snapshot(Oldest(pendingLighting, lightChannel)),
        Meshing = meshingProfile.Snapshot(Oldest(pendingMeshing, meshChannel)),
        ResultsQueued = resultChannel.Reader.Count,
        StaleResults = staleResults
    };

    private static double Oldest(Queue<WorkItem> pending, Channel<WorkItem> channel)
    {
        long earliest = long.MaxValue;
        if (pending.TryPeek(out var p)) earliest = p.QueuedAt;
        if (channel.Reader.TryPeek(out var c)) earliest = Math.Min(earliest, c.QueuedAt);
        return earliest == long.MaxValue ? 0 : Stopwatch.GetElapsedTime(earliest).TotalMilliseconds;
    }

    public bool Tick()
    {
        long tickStarted = Stopwatch.GetTimestamp();
        tickDeadline = tickStarted + (long)(TickBudgetMilliseconds * Stopwatch.Frequency / 1000.0);
        
        MarkForDeletion();

        long completionStarted = Stopwatch.GetTimestamp();
        int chunksMeshed = DrainResults();
        CompletionMilliseconds = Stopwatch.GetElapsedTime(completionStarted).TotalMilliseconds;
        
        Flush();
        ProcessFresh();

        int chunksDeleted = ProcessDeletion();

        ScanStrandedRecords();

        return chunksMeshed > 0 || chunksDeleted > 0;
    }

    private bool BudgetExhausted(int processed, int minimum)
    {
        if (processed >= MaxItemsPerPhase) return true;
        if (processed < minimum) return false;
        return Stopwatch.GetTimestamp() >= tickDeadline;
    }

    public void AddToRegistry(Chunk chunk, ChunkStage stage)
    {
        ChunkRecord record = new()
        {
            Version = NextVersion,
            Chunk = chunk,
            Stage = stage,
            Flags = ChunkFlags.Wanted
        };

        registry.TryAdd(chunk.Index, record);
    }

    private int DrainResults()
    {
        int chunksMeshed = 0;

        for (int processed = 0;
             !BudgetExhausted(processed, MinRelights) && relightChannel.Reader.TryRead(out var relightRequest);
             processed++)
        {
            ProcessRelightRequest(relightRequest);
        }

        for (int processed = 0;
             !BudgetExhausted(processed, MinResults) && resultChannel.Reader.TryRead(out var result);
             processed++)
        {
            if (result is { JobType: JobType.Meshing, Status: ResultStatus.Success })
            {
                chunksMeshed++;
            }

            ProcessResult(result);
        }

        return chunksMeshed;
    }

    private void ProcessResult(WorkResult result)
    {
        if (!registry.TryGetValue(result.Index, out var record))
        {
            staleResults++;
            return;
        }

        record.InFlight = null;

        if (result.Version != record.Version)
        {
            staleResults++;
            if (!record.Flags.HasFlag(ChunkFlags.Wanted) && record.Chunk is null)
            {
                registry.TryRemove(result.Index, out _);
            }
            else
            {
                TryDispatch(result.Index);
            }

            return;
        }

        Advance(record, result);
    }

    private void Advance(ChunkRecord record, WorkResult result)
    {
        if (!record.Flags.HasFlag(ChunkFlags.Wanted) || record.Flags.HasFlag(ChunkFlags.DeleteRequested))
        {
            return;
        }

        if (result.Status == ResultStatus.Success)
        {
            switch (result.JobType)
            {
                case JobType.Generation:
                    record.Chunk = result.Chunk;
                    record.Chunk!.EnsureLight(); // The worker already allocated it
                    volume.Add(record.Chunk!);
                    record.Stage = ChunkStage.Generated;
                    break;
                case JobType.Lighting:
                    // A relight that changed nothing in this chunk keeps the existing mesh.
                    // Neighbor-boundary changes that do affect it arrive as NeedsRemesh instead.
                    if (record.Stage < ChunkStage.Lit || (result.LightChanged && !record.Chunk!.IsEmpty))
                    {
                        record.Stage = ChunkStage.Lit;
                    }
                    break;
                case JobType.Meshing:
                    record.Stage = ChunkStage.Meshed;

                    if (result.Mesh.HasValue)
                    {
                        var meshUpdate = new MeshUpdate(result.Index, MeshUpdateState.Updated,
                            result.Mesh.Value.Opaque, result.Mesh.Value.Transparent);
                        meshSink.AddMesh(meshUpdate);
                        record.Chunk?.IsReady = true;
                    }

                    break;
            }

            TryDispatch(result.Index);

            if (record.Chunk is not null && result.JobType is JobType.Lighting or JobType.Generation)
            {
                foreach (var offset in NeighborOffsets)
                {
                    TryDispatch(result.Index + offset);
                }
            }
        }
        else if (result.Status == ResultStatus.Skip)
        {
            TryDispatch(result.Index);
        }
        else if (result.Status == ResultStatus.Fail)
        {
            Console.WriteLine($"Job failed for chunk {result.Index}, stage: {record.Stage}, error: {result.Exception}");
        }
    }

    /// Returns false if the target channel is full
    private bool TryDispatch(Vec3<int> index)
    {
        if (!volume.IsWithinActiveVolume(index))
            return true;

        if (!registry.TryGetValue(index, out var record))
            return true;

        if (!record.Flags.HasFlag(ChunkFlags.Wanted))
            return true;

        if (record.InFlight != null)
            return true;

        JobType? job = record.Stage switch
        {
            ChunkStage.Fresh => JobType.Generation,
            ChunkStage.Generated => JobType.Lighting,
            ChunkStage.Lit => JobType.Meshing,
            _ => null
        };

        if (record.Dirty.HasFlag(ChunkDirty.NeedsRelight) && record.Stage >= ChunkStage.Lit)
        {
            job = JobType.Lighting;
        }
        else if (record.Dirty.HasFlag(ChunkDirty.NeedsRemesh) && record.Stage >= ChunkStage.Lit)
        {
            job = JobType.Meshing;
        }

        if (job is null)
            return true;

        // Generation jobs are dispatched separately as a special case
        if (job == JobType.Generation)
        {
            return Dispatch(record, index, JobType.Generation, new NeighborSet());
        }

        if (record.Chunk is null)
            return true;

        // Empty chunks have no geometry and need no neighbors
        if (job == JobType.Meshing && record.Chunk.IsEmpty)
        {
            CompleteEmptyMesh(record, index);
            return true;
        }

        bool allNeighborsExist = TryCollectNeighbors(record.Chunk, out var neighbors, out var records);

        if (job == JobType.Meshing && !(allNeighborsExist && AllNeighborsLit(records)))
            return true;

        return Dispatch(record, index, job.Value, neighbors);
    }

    private void CompleteEmptyMesh(ChunkRecord record, Vec3<int> index)
    {
        bool firstMesh = record.Stage < ChunkStage.Meshed;

        record.Dirty &= ~ChunkDirty.NeedsRemesh;
        record.Stage = ChunkStage.Meshed;
        record.Chunk!.IsReady = true;

        // The renderer drops any existing entry for a zero-face update
        if (!firstMesh)
        {
            meshSink.AddMesh(new MeshUpdate(index, MeshUpdateState.Updated, [], []));
        }
    }

    private bool Dispatch(ChunkRecord record, Vec3<int> index, JobType job, in NeighborSet neighbors)
    {
        record.InFlight = job;
        var work = new WorkItem(index, record.Version, record.Chunk, neighbors);
        var (channel, pending) = job switch
        {
            JobType.Generation => (genChannel, pendingGeneration),
            JobType.Lighting => (lightChannel, pendingLighting),
            JobType.Meshing => (meshChannel, pendingMeshing),
            _ => throw new ArgumentOutOfRangeException(nameof(job))
        };

        if (channel.Writer.TryWrite(work))
        {
            if (job == JobType.Lighting)
                record.Dirty &= ~ChunkDirty.NeedsRelight;
            else if (job == JobType.Meshing)
                record.Dirty &= ~ChunkDirty.NeedsRemesh;

            return true;
        }

        pending.Enqueue(work);
        return false;
    }

    private void ProcessRelightRequest(RelightRequest request)
    {
        if (!registry.TryGetValue(request.Index, out var record)) return;
        if (!record.Flags.HasFlag(ChunkFlags.Wanted)) return;
        if (record.Chunk is null) return;

        // A neighbor's boundary light changed. Faces of this chunk sample that boundary,
        // so a non-empty chunk needs a remesh even if its own light map ends up unchanged
        record.Dirty |= ChunkDirty.NeedsRelight;
        if (!record.Chunk.IsEmpty)
        {
            record.Dirty |= ChunkDirty.NeedsRemesh;
        }

        TryDispatch(request.Index);
    }

    private void MarkForDeletion()
    {
        for (int processed = 0;
             !BudgetExhausted(processed, MinMarks) && markedForDeletionQueue.TryDequeue(out var id);
             processed++)
        {
            if (!registry.TryGetValue(id, out var record))
                continue;

            if (record.Flags.HasFlag(ChunkFlags.DeleteRequested))
                continue;

            if (volume.IsWithinActiveVolume(id))
                continue;

            pendingDeletion.Add(id);

            record.Flags &= ~ChunkFlags.Wanted;
            record.Flags |= ChunkFlags.DeleteRequested;
            record.Version = NextVersion;
        }
    }

    private int ProcessDeletion()
    {
        // Refill only once the previous batch is fully handled, so nothing is queued twice.
        // Entries skipped below stay in pendingDeletion and are picked up by a later refill
        if (deletionQueue.Count == 0)
        {
            if (pendingDeletion.Count == 0) return 0;

            foreach (var id in pendingDeletion)
            {
                if (deletionQueue.Count >= MaxItemsPerPhase) break;
                deletionQueue.Enqueue(id);
            }
        }

        int chunksDeleted = 0;

        for (int processed = 0;
             !BudgetExhausted(processed, MinDeletions) && deletionQueue.TryDequeue(out var id);
             processed++)
        {
            if (!registry.TryGetValue(id, out var record)
                || record.Flags.HasFlag(ChunkFlags.Wanted))
            {
                pendingDeletion.Remove(id);
                continue;
            }

            if (record.InFlight != null)
                continue;

            if (record.Chunk is not null && AnyNeighborInFlight(record.Chunk))
                continue;

            if (record.Chunk is { } chunk)
            {
                meshSink.RemoveMesh(chunk.Index);
                volume.RemoveChunk(chunk.Index);
                chunk.IsReady = false;
            }

            registry.TryRemove(id, out _);
            pendingDeletion.Remove(id);
            chunksDeleted++;
        }

        return chunksDeleted;
    }

    private void ScanStrandedRecords()
    {
        strandedScan ??= registry.GetEnumerator();

        for (int i = 0; i < StrandedScanPerTick; i++)
        {
            if (!strandedScan.MoveNext())
            {
                strandedScan.Dispose();
                strandedScan = null;
                return;
            }

            var (id, record) = strandedScan.Current;
            if (record.Chunk is null && record.InFlight is null && !volume.IsWithinActiveVolume(id))
            {
                registry.TryRemove(id, out _);
            }
        }
    }

    private bool AnyNeighborInFlight(Chunk chunk)
    {
        foreach (var offset in NeighborOffsets)
        {
            if (!registry.TryGetValue(chunk.Index + offset, out var nRecord))
                continue;

            if (!nRecord.Flags.HasFlag(ChunkFlags.Wanted))
                continue;

            if (nRecord.InFlight != null)
                return true;
        }

        return false;
    }

    private void Flush()
    {
        if (pendingGeneration.Count > 0)
        {
            FlushQueue(pendingGeneration, JobType.Generation);
        }

        if (pendingLighting.Count > 0)
        {
            FlushQueue(pendingLighting, JobType.Lighting);
        }

        if (pendingMeshing.Count > 0)
        {
            FlushQueue(pendingMeshing, JobType.Meshing);
        }
    }

    private void FlushQueue(Queue<WorkItem> pending, JobType job)
    {
        for (int processed = 0; !BudgetExhausted(processed, MinFlush); processed++)
        {
            if (!pending.TryDequeue(out var item))
                break;

            if (!registry.TryGetValue(item.Index, out var record)) continue;

            if (record.InFlight == job)
            {
                record.InFlight = null;
            }

            if (record.Version != item.Version && !record.Flags.HasFlag(ChunkFlags.Wanted))
                continue;

            bool channelWriteSuccessful = TryDispatch(item.Index);
            if (!channelWriteSuccessful) break;
        }
    }

    private void ProcessFresh()
    {
        for (int processed = 0;
             !BudgetExhausted(processed, MinFresh) && freshQueue.TryDequeue(out var id);
             processed++)
        {
            if (!volume.IsWithinActiveVolume(id)) continue;

            if (!registry.TryGetValue(id, out var record))
            {
                record = new ChunkRecord
                {
                    Version = NextVersion,
                    Stage = ChunkStage.Fresh,
                    Flags = ChunkFlags.Wanted
                };

                registry[id] = record;
            }
            else
            {
                bool wasUnwanted = !record.Flags.HasFlag(ChunkFlags.Wanted)
                                   || record.Flags.HasFlag(ChunkFlags.DeleteRequested);

                record.Flags |= ChunkFlags.Wanted;
                record.Flags &= ~ChunkFlags.DeleteRequested;

                if (wasUnwanted)
                {
                    record.Version = NextVersion;
                    record.Dirty |= ChunkDirty.NeedsRelight;
                }
            }

            bool channelWriteSuccessful = TryDispatch(id);
            if (!channelWriteSuccessful) break;
        }
    }

    private async Task GenerateChunkAsync(CancellationToken ct)
    {
        await foreach (var item in genChannel.Reader.ReadAllAsync(ct))
        {
            using var measurement = generationProfile.Start(item.QueuedAt);
            var result = new WorkResult(item.Index, item.Version, JobType.Generation, ResultStatus.Skip, null, null,null);
            try
            {
                var chunk = chunkGenerator.GenerateChunk(item.Index);
                chunk.EnsureLight();
                result.Chunk = chunk;
                result.Status = ResultStatus.Success;
            }
            catch (Exception ex) when (ex is not (TaskCanceledException or OperationCanceledException))
            {
                Console.WriteLine(ex);
                result.Exception = ex;
                result.Status = ResultStatus.Fail;
            }

            generationProfile.Outcome(result.Status == ResultStatus.Success, result.Status == ResultStatus.Fail);
            await resultChannel.Writer.WriteAsync(result, ct);
        }
    }

    private async Task LightChunkAsync(CancellationToken ct)
    {
        await foreach (var item in lightChannel.Reader.ReadAllAsync(ct))
        {
            using var measurement = lightingProfile.Start(item.QueuedAt);
            var (index, version, chunk, neighborSet) = item;
            var result = new WorkResult(index, version, JobType.Lighting, ResultStatus.Skip, chunk, null, null);
            try
            {
                if (chunk is null || neighborSet is null)
                {
                    lightingProfile.Outcome(result.Status == ResultStatus.Success, result.Status == ResultStatus.Fail);
                    await resultChannel.Writer.WriteAsync(result, ct);
                    continue;
                }

                // Seeding and flooding share a thread-local scratch queue and must stay free of awaits
                ChunkLight.ResetThreadScratch();

                if (chunkGenerator.IsSunlight(chunk))
                {
                    chunk.Light!.SeedSkylight();
                }

                if (!chunk.IsEmpty)
                {
                    chunk.Light!.SeedBlockLight();
                }

                chunk.Light!.SeedNeighborsLight(neighborSet.Value);

                FacesState changed = chunk.Light!.Flood(out bool lightChanged);
                result.LightChanged = lightChanged;

                if (changed.ZPos)
                    await relightChannel.Writer.WriteAsync(new RelightRequest(index + new Vec3<int>(0, 0, 1)), ct);
                if (changed.ZNeg)
                    await relightChannel.Writer.WriteAsync(new RelightRequest(index + new Vec3<int>(0, 0, -1)), ct);
                if (changed.XPos)
                    await relightChannel.Writer.WriteAsync(new RelightRequest(index + new Vec3<int>(1, 0, 0)), ct);
                if (changed.XNeg)
                    await relightChannel.Writer.WriteAsync(new RelightRequest(index + new Vec3<int>(-1, 0, 0)), ct);
                if (changed.YPos)
                    await relightChannel.Writer.WriteAsync(new RelightRequest(index + new Vec3<int>(0, 1, 0)), ct);
                if (changed.YNeg)
                    await relightChannel.Writer.WriteAsync(new RelightRequest(index + new Vec3<int>(0, -1, 0)), ct);

                result.Chunk = chunk;
                result.Status = ResultStatus.Success;
            }
            catch (Exception ex) when (ex is not (TaskCanceledException or OperationCanceledException))
            {
                Console.WriteLine(ex);
                result.Exception = ex;
                result.Status = ResultStatus.Fail;
            }

            lightingProfile.Outcome(result.Status == ResultStatus.Success, result.Status == ResultStatus.Fail);
            await resultChannel.Writer.WriteAsync(result, ct);
        }
    }

    private async Task MeshChunkAsync(CancellationToken ct)
    {
        await foreach (var item in meshChannel.Reader.ReadAllAsync(ct))
        {
            using var measurement = meshingProfile.Start(item.QueuedAt);
            var result = new WorkResult(item.Index, item.Version, JobType.Meshing, ResultStatus.Skip, item.Chunk,  null,null);
            try
            {
                if (item.Chunk is null || item.Neighbors is null || item.Neighbors is { All: false })
                {
                    meshingProfile.Outcome(result.Status == ResultStatus.Success, result.Status == ResultStatus.Fail);
                    await resultChannel.Writer.WriteAsync(result, ct);
                    continue;
                }

                var (opaque, transparent) = chunkMesher.Build(item.Chunk, item.Neighbors.Value);
                var meshingResult = new MeshingResult(opaque, transparent);
                result.Mesh = meshingResult;
                result.Status = ResultStatus.Success;
            }
            catch (Exception ex) when (ex is not (TaskCanceledException or OperationCanceledException))
            {
                Console.WriteLine(ex);
                result.Exception = ex;
                result.Status = ResultStatus.Fail;
            }

            meshingProfile.Outcome(result.Status == ResultStatus.Success, result.Status == ResultStatus.Fail);
            await resultChannel.Writer.WriteAsync(result, ct);
        }
    }

    private bool TryCollectNeighbors(Chunk chunk, out NeighborSet neighbors, out FacesData<ChunkRecord> records)
    {
        registry.TryGetValue(chunk.Index + new Vec3<int>(0, 0, 1), out var zPos);
        registry.TryGetValue(chunk.Index + new Vec3<int>(0, 0, -1), out var zNeg);
        registry.TryGetValue(chunk.Index + new Vec3<int>(1, 0, 0), out var xPos);
        registry.TryGetValue(chunk.Index + new Vec3<int>(-1, 0, 0), out var xNeg);
        registry.TryGetValue(chunk.Index + new Vec3<int>(0, 1, 0), out var yPos);
        registry.TryGetValue(chunk.Index + new Vec3<int>(0, -1, 0), out var yNeg);

        neighbors = new NeighborSet
        {
            ZPos = zPos?.Chunk,
            ZNeg = zNeg?.Chunk,
            XPos = xPos?.Chunk,
            XNeg = xNeg?.Chunk,
            YPos = yPos?.Chunk,
            YNeg = yNeg?.Chunk
        };

        // Guaranteed to be not null for the consumer if the method returns 'true'
        records = new FacesData<ChunkRecord>
        {
            ZPos = zPos!,
            ZNeg = zNeg!,
            XPos = xPos!,
            XNeg = xNeg!,
            YPos = yPos!,
            YNeg = yNeg!
        };

        return neighbors.All;
    }

    private static bool AllNeighborsLit(FacesData<ChunkRecord> records)
    {
        return records.ZPos.Stage >= ChunkStage.Lit
               && records.ZNeg.Stage >= ChunkStage.Lit
               && records.XPos.Stage >= ChunkStage.Lit
               && records.XNeg.Stage >= ChunkStage.Lit
               && records.YPos.Stage >= ChunkStage.Lit
               && records.YNeg.Stage >= ChunkStage.Lit;
    }

    // Disposal
    private bool disposed;

    public void Dispose()
    {
        if (disposed) return;
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;

        await cts.CancelAsync();

        try
        {
            var allWorkers = genWorkers.Concat(lightingWorkers).Concat(meshingWorkers);
            await Task.WhenAll(allWorkers);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Worker faulted during disposal: {ex}");
        }

        strandedScan?.Dispose();
        cts.Dispose();

        GC.SuppressFinalize(this);
    }

    private record struct WorkItem(Vec3<int> Index, ulong Version, Chunk? Chunk, NeighborSet? Neighbors)
    {
        public long QueuedAt { get; } = Stopwatch.GetTimestamp();
    }

    private record struct WorkResult(
        Vec3<int> Index,
        ulong Version,
        JobType JobType,
        ResultStatus Status,
        Chunk? Chunk,
        MeshingResult? Mesh,
        Exception? Exception)
    {
        // Lighting only: whether the flood changed any value in this chunk's light map
        public bool LightChanged { get; set; }
    }

    private record struct MeshingResult(VoxelFace[] Opaque, VoxelFace[] Transparent);

    private record struct RelightRequest(Vec3<int> Index);

    private enum ResultStatus
    {
        Success,
        Fail,
        Skip
    }
}

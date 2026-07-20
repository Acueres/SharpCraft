using SharpCraft.SharpMath;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.World.Meshing;
using SharpCraft.World.Generation;

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SharpCraft.World.WorldStreaming;

internal class ChunkPipeline : IDisposable, IAsyncDisposable
{
    private const int ChunkBudget = 500;
    private ulong NextVersion => Interlocked.Increment(ref field);

    private readonly ChunkVolume volume;
    private readonly ChunkGenerator chunkGenerator;
    private readonly ChunkMesher chunkMesher;

    private readonly CancellationTokenSource cts = new();

    private readonly ConcurrentDictionary<Vec3<int>, ChunkRecord> registry = [];
    private readonly Queue<WorkItem> pendingGeneration = [];
    private readonly Queue<WorkItem> pendingLighting = [];
    private readonly Queue<WorkItem> pendingMeshing = [];
    private readonly HashSet<Vec3<int>> pendingDeletion = [];
    private readonly Queue<Vec3<int>> deletionQueue = [];

    private const int StrandedCleanupPeriodTicks = 500;
    
    private int ticksSinceLastStrandedCleanup;

    private readonly Channel<WorkItem> genChannel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(ChunkBudget)
    {
        SingleReader = false,
        SingleWriter = true
    });
    private readonly Channel<WorkItem> lightChannel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(ChunkBudget)
    {
        SingleReader = false,
        SingleWriter = true
    });
    private readonly Channel<WorkItem> meshChannel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(ChunkBudget)
    {
        SingleReader = false,
        SingleWriter = true
    });

    private readonly Channel<WorkResult> resultChannel = Channel.CreateBounded<WorkResult>(new BoundedChannelOptions(ChunkBudget)
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Channel<RelightRequest> relightChannel = Channel.CreateBounded<RelightRequest>(new BoundedChannelOptions(ChunkBudget)
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Task[] genWorkers;
    private readonly Task[] lightingWorkers;
    private readonly Task[] meshingWorkers;

    public ChunkPipeline(ChunkVolume volume, ChunkGenerator chunkGenerator, ChunkMesher chunkMesher)
    {
        this.volume = volume;
        this.chunkGenerator = chunkGenerator;
        this.chunkMesher = chunkMesher;
        
        CancellationToken ct = cts.Token;
        
        int computeBudget = Math.Max(1, Environment.ProcessorCount - 2);
        int genWorkersCount   = Math.Max(1, computeBudget / 2);
        int lightWorkersCount = Math.Max(1, computeBudget / 4);
        int meshWorkersCount  = Math.Max(1, computeBudget - genWorkersCount - lightWorkersCount);

        genWorkers = Enumerable.Range(0, genWorkersCount).Select(_ => Task.Run(() => GenerateChunkAsync(ct), ct)).ToArray();
        lightingWorkers = Enumerable.Range(0, lightWorkersCount).Select(_ => Task.Run(() => LightChunkAsync(ct), ct)).ToArray();
        meshingWorkers = Enumerable.Range(0, meshWorkersCount).Select(_ => Task.Run(() => MeshChunkAsync(ct), ct)).ToArray();
    }

    public void Schedule(List<Vec3<int>> toGenerate, List<Vec3<int>> toRemove)
    {
        MarkForDeletion(toRemove);
        ProcessFresh(toGenerate);
    }

    public bool Tick()
    {
        int chunksMeshed = DrainResults();
        
        Flush();
        
        int chunksDeleted = ProcessDeletion();

        if (++ticksSinceLastStrandedCleanup >= StrandedCleanupPeriodTicks)
        {
            ClearStrandedRecords();
            ticksSinceLastStrandedCleanup = 0;
        }

        return chunksMeshed > 0 || chunksDeleted > 0;
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

        while (relightChannel.Reader.TryRead(out var relightRequest))
        {
            ProcessRelightRequest(relightRequest);
        }

        while (resultChannel.Reader.TryRead(out var result))
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
            return;

        record.InFlight = null;

        if (result.Version != record.Version)
        {
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
                    record.Chunk!.EnsureLight();
                    volume.Add(record.Chunk!);
                    record.Stage = ChunkStage.Generated;
                    break;
                case JobType.Lighting:
                    record.Stage = ChunkStage.Lit;
                    break;
                case JobType.Meshing:
                    record.Stage = ChunkStage.Meshed;
                    record.Chunk?.IsReady = true;
                    break;
            }

            TryDispatch(result.Index);

            if (record.Chunk is not null && result.JobType is JobType.Lighting or JobType.Generation)
            {
                foreach (var n in record.Chunk.GetNeighborIndexes())
                {
                    TryDispatch(n);
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

    private void TryDispatch(Vec3<int> index)
    {
        if (!volume.IsWithinActiveVolume(index))
            return;

        if (!registry.TryGetValue(index, out var record))
            return;

        if (!record.Flags.HasFlag(ChunkFlags.Wanted))
            return;

        if (record.InFlight != null)
            return;

        JobType? job = record.Stage switch
        {
            ChunkStage.Fresh     => JobType.Generation,
            ChunkStage.Generated => JobType.Lighting,
            ChunkStage.Lit       => JobType.Meshing,
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
            return;

        // Generation jobs are dispatched separately as a special case
        if (job == JobType.Generation)
        {
            Dispatch(record, index, JobType.Generation, new NeighborSet());
            return;
        }

        if (record.Chunk is null)
            return;

        bool allNeighborsExist = TryCollectNeighbors(record.Chunk, out var neighbors, out var records);

        if (job == JobType.Meshing && !(allNeighborsExist && AllNeighborsLit(records)))
            return;
        
        Dispatch(record, index, job.Value, neighbors);
    }

    private void Dispatch(ChunkRecord record, Vec3<int> index, JobType job, in NeighborSet neighbors)
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
        }
        else
        {
            pending.Enqueue(work);
        }
    }

    private void ProcessRelightRequest(RelightRequest request)
    {
        if (!registry.TryGetValue(request.Index, out var record)) return;
        if (!record.Flags.HasFlag(ChunkFlags.Wanted)) return;
        if (record.Chunk is null) return;

        record.Dirty |= ChunkDirty.NeedsRelight;
        TryDispatch(request.Index);
    }

    private void MarkForDeletion(List<Vec3<int>> toRemove)
    {
        foreach (var id in toRemove)
        {
            if (!registry.TryGetValue(id, out var record))
                continue;
            
            if (record.Flags.HasFlag(ChunkFlags.DeleteRequested))
                continue;
            
            pendingDeletion.Add(id);

            record.Flags &= ~ChunkFlags.Wanted;
            record.Flags |= ChunkFlags.DeleteRequested;
            record.Version = NextVersion;
        }
    }

    private int ProcessDeletion()
    {
        if (pendingDeletion.Count == 0) return 0;

        foreach (var id in pendingDeletion)
        {
            deletionQueue.Enqueue(id);
        }

        int chunksDeleted = 0;

        while (deletionQueue.TryDequeue(out var id))
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
                chunkMesher.Remove(chunk.Index);
                volume.RemoveChunk(chunk.Index);
                chunk.IsReady = false;
            }

            registry.TryRemove(id, out _);
            pendingDeletion.Remove(id);
            chunksDeleted++;
        }

        return chunksDeleted;
    }

    private void ClearStrandedRecords()
    {
        foreach (var (id, record) in registry)
        {
            if (record.Chunk is null && record.InFlight is null && !volume.IsWithinActiveVolume(id))
            {
                registry.TryRemove(id, out _);
                Console.WriteLine($"Cleared stranded record {id}");
            }
        }
    }

    private bool AnyNeighborInFlight(Chunk chunk)
    {
        foreach (var nIdx in chunk.GetNeighborIndexes())
        {
            if (!registry.TryGetValue(nIdx, out var nRecord))
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
        for (int i = 0; i < ChunkBudget; i++)
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

            TryDispatch(item.Index);
        }
    }

    private void ProcessFresh(List<Vec3<int>> toGenerate)
    {
        foreach (var index in toGenerate)
        {
            if (!registry.TryGetValue(index, out var record))
            {
                record = new ChunkRecord
                {
                    Version = NextVersion,
                    Stage = ChunkStage.Fresh,
                    Flags = ChunkFlags.Wanted
                };

                registry[index] = record;
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

            TryDispatch(index);
        }
    }

    private async Task GenerateChunkAsync(CancellationToken ct)
    {
        await foreach (var item in genChannel.Reader.ReadAllAsync(ct))
        {
            var result = new WorkResult(item.Index, item.Version, JobType.Generation, ResultStatus.Skip, null, null);
            try
            {
                result.Chunk = chunkGenerator.GenerateChunk(item.Index);
                result.Status = ResultStatus.Success;
            }
            catch (Exception ex) when (ex is not (TaskCanceledException or OperationCanceledException))
            {
                Console.WriteLine(ex);
                result.Exception = ex;
                result.Status = ResultStatus.Fail;
            }

            await resultChannel.Writer.WriteAsync(result, ct);
        }
    }

    private async Task LightChunkAsync(CancellationToken ct)
    {
        await foreach (var (index, version, chunk, neighborSet) in lightChannel.Reader.ReadAllAsync(ct))
        {
            var result = new WorkResult(index, version, JobType.Lighting, ResultStatus.Skip, chunk, null);
            try
            {
                if (chunk is null || neighborSet is null)
                {
                    await resultChannel.Writer.WriteAsync(result, ct);
                    continue;
                }
                
                if (chunkGenerator.IsSunlight(chunk))
                {
                    chunk.Light!.SeedSkylight();
                }

                if (!chunk.IsEmpty)
                {
                    chunk.Light!.SeedBlockLight();
                }
                
                chunk.Light!.SeedNeighborsLight(neighborSet.Value);

                FacesState changed = chunk.Light!.Flood();
                
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

            await resultChannel.Writer.WriteAsync(result, ct);
        }
    }

    private async Task MeshChunkAsync(CancellationToken ct)
    {
        await foreach (var item in meshChannel.Reader.ReadAllAsync(ct))
        {
            var result = new WorkResult(item.Index, item.Version, JobType.Meshing, ResultStatus.Skip, item.Chunk, null);
            try
            {
                if (item.Chunk is null || item.Neighbors is null || item.Neighbors is { All: false })
                {
                    await resultChannel.Writer.WriteAsync(result, ct);
                    continue;
                }

                chunkMesher.Build(item.Chunk, item.Neighbors.Value);
                result.Status = ResultStatus.Success;
            }
            catch (Exception ex) when (ex is not (TaskCanceledException or OperationCanceledException))
            {
                Console.WriteLine(ex);
                result.Exception = ex;
                result.Status = ResultStatus.Fail;
            }

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
        disposed = true;

        cts.Cancel();
        cts.Dispose();

        GC.SuppressFinalize(this);
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
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Worker faulted during disposal: {ex}");
        }

        cts.Dispose();

        GC.SuppressFinalize(this);
    }

    private record struct WorkItem(Vec3<int> Index, ulong Version, Chunk? Chunk, NeighborSet? Neighbors);
    
    private record struct WorkResult(Vec3<int> Index, ulong Version, JobType JobType, ResultStatus Status, Chunk? Chunk, Exception? Exception);

    private record struct RelightRequest(Vec3<int> Index);

    private enum ResultStatus
    {
        Success,
        Fail,
        Skip
    }
}
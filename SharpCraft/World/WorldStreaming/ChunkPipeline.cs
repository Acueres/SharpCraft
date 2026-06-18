using SharpCraft.SharpMath;
using SharpCraft.World.Chunks;
using SharpCraft.World.Meshing;
using SharpCraft.World.Generation;
using SharpCraft.World.Lighting;

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SharpCraft.World.WorldStreaming;

internal class ChunkPipeline : IDisposable, IAsyncDisposable
{
    private const int ChunkCapacity = 500;
    private ulong NextVersion => Interlocked.Increment(ref field);

    private readonly ChunkVolume volume;
    private readonly ChunkLinker linker;
    private readonly ChunkGenerator chunkGenerator;
    private readonly ChunkMesher chunkMesher;

    private readonly CancellationTokenSource cts = new();

    private readonly ConcurrentDictionary<Vec3<int>, ChunkRecord> registry = [];
    private readonly Queue<WorkItem> pendingGeneration = [];
    private readonly Queue<WorkItem> pendingLinking = [];
    private readonly Queue<WorkItem> pendingLighting = [];
    private readonly Queue<WorkItem> pendingMeshing = [];

    private readonly Channel<WorkItem> genChannel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(ChunkCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly Channel<WorkItem> linkChannel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(ChunkCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly Channel<WorkItem> lightChannel = Channel.CreateBounded <WorkItem>(new BoundedChannelOptions(ChunkCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly Channel<WorkItem> meshChannel = Channel.CreateBounded <WorkItem>(new BoundedChannelOptions(ChunkCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly Channel<WorkResult> resultChannel = Channel.CreateBounded<WorkResult>(new BoundedChannelOptions(ChunkCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly Task genWorker;
    private readonly Task linkingWorker;
    private readonly Task lightingWorker;
    private readonly Task meshingWorker;

    public ChunkPipeline(ChunkVolume volume, ChunkGenerator chunkGenerator, ChunkMesher chunkMesher)
    {
        this.volume = volume;
        this.chunkGenerator = chunkGenerator;
        this.chunkMesher = chunkMesher;

        linker = new ChunkLinker();

        CancellationToken ct = cts.Token;

        genWorker = Task.Run(() => GenerateChunkAsync(ct), ct);
        linkingWorker = Task.Run(() => LinkChunkAsync(ct), ct);
        lightingWorker = Task.Run(() => LightChunkAsync(ct), ct);
        meshingWorker = Task.Run(() => MeshChunkAsync(ct), ct);
    }

    public void Schedule(List<Vec3<int>> toGenerate, List<Vec3<int>> toRemove)
    {
        MarkForDeletion(toRemove);
        ProcessFresh(toGenerate);
        ProcessDeletion(toRemove);
    }

    public void Tick()
    {
        DrainResults();
        Flush();
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

    public void AddToWorker(Chunk chunk, JobType job)
    {
        if (!registry.TryGetValue(chunk.Index, out var record)) return;

        var work = new WorkItem(chunk.Index, record.Version, chunk, chunk.Neighbors);

        switch (job)
        {
            case JobType.Generation:
                if (!genChannel.Writer.TryWrite(work))
                    pendingGeneration.Enqueue(work);
                break;
            case JobType.Linking:
                if (!linkChannel.Writer.TryWrite(work))
                    pendingLinking.Enqueue(work);
                break;
            case JobType.Lighting:
                if (!lightChannel.Writer.TryWrite(work))
                    pendingLighting.Enqueue(work);
                break;
            case JobType.Meshing:
                if (!meshChannel.Writer.TryWrite(work))
                    pendingMeshing.Enqueue(work);
                break;
        }
    }

    private void DrainResults()
    {
        while (resultChannel.Reader.TryRead(out var result))
        {
            ProcessResult(result);
        }
    }

    private void ProcessResult(WorkResult result)
    {
        if (!registry.TryGetValue(result.Index, out var record))
            return;

        if (result.Version != record.Version)
        {
            if (!record.Flags.HasFlag(ChunkFlags.Wanted) &&
                record.Chunk is null)
            {
                registry.TryRemove(result.Index, out _);
            }
            return;
        }

        switch (result.Status)
        {
            case ResultStatus.Skipped:
                HandleSkipped(record, result);
                return;

            case ResultStatus.Failed:
                HandleFailed(record, result);
                return;

            case ResultStatus.Success:
                HandleSuccess(record, result);
                return;

            case ResultStatus.RelightRequested:
                HandleRelight(result.Index, result.Version);
                return;
            case ResultStatus.RemeshRequested:
                HandleRemesh(result.Index, result.Version);
                return;
        }
    }

    private void HandleSkipped(ChunkRecord record, WorkResult result)
    {
        if (!record.Flags.HasFlag(ChunkFlags.Wanted) ||
            record.Flags.HasFlag(ChunkFlags.DeleteRequested))
        {
            record.Stage = result.JobType switch
            {
                JobType.Generation => ChunkStage.Fresh,
                JobType.Linking or JobType.Lighting => ChunkStage.Generated,
                JobType.Meshing => ChunkStage.Lit,
                _ => record.Stage
            };

            return;
        }

        switch (record.Stage)
        {
            case ChunkStage.Generating:
                pendingGeneration.Enqueue(new WorkItem(result.Index, record.Version, null, null));
                break;
            case ChunkStage.Linking:
                pendingLinking.Enqueue(new WorkItem(result.Index, record.Version, result.Chunk, null));
                break;
            case ChunkStage.Lighting:
                pendingLighting.Enqueue(new WorkItem(result.Index, record.Version, result.Chunk, result.Chunk?.Neighbors));
                break;
            case ChunkStage.Meshing:
                pendingMeshing.Enqueue(new WorkItem(result.Index, record.Version, result.Chunk, result.Chunk?.Neighbors));
                break;
        }
    }

    private static void HandleFailed(ChunkRecord record, WorkResult result)
    {
        Console.WriteLine($"Job failed for chunk {result.Index}, stage: {record.Stage}, error: {result.Exception}");
    }

    private void HandleSuccess(ChunkRecord record, WorkResult result)
    {
        switch (result.JobType)
        {
            case JobType.Generation:
                if (!record.Flags.HasFlag(ChunkFlags.Wanted) ||
                    record.Flags.HasFlag(ChunkFlags.DeleteRequested))
                {
                    registry.TryRemove(result.Index, out _);
                    break;
                }

                record.Stage = ChunkStage.Generated;
                record.Chunk = result.Chunk;
                
                volume.TryAdd(result.Chunk!);

                TryScheduleLinking(result.Index);

                foreach (var neighborIndex in result.Chunk!.GetNeighborIndexes())
                {
                    TryScheduleLinking(neighborIndex);
                    TryScheduleMeshing(neighborIndex);
                }

                break;
            case JobType.Linking:
                record.Stage = ChunkStage.Lighting;
                
                var work = new WorkItem(result.Index, record.Version, result.Chunk, result.Chunk?.Neighbors);
                
                if (!lightChannel.Writer.TryWrite(work))
                    pendingLighting.Enqueue(work);
                break;
            case JobType.Lighting:
                record.Stage = ChunkStage.Lit;
                TryScheduleMeshing(result.Index);
                break;
            case JobType.Meshing:
                record.Stage = ChunkStage.Meshed;
                record.Chunk?.IsReady = true;
                break;
        }
    }

    private void TryScheduleLinking(Vec3<int> index)
    {
        if (!registry.TryGetValue(index, out var record))
            return;

        if (record.Stage != ChunkStage.Generated)
            return;

        if (!record.Flags.HasFlag(ChunkFlags.Wanted))
            return;

        if (record.Chunk is null)
            return;

        if (!TryCollectNeighbors(record.Chunk, out var neighbors))
            return;

        record.Stage = ChunkStage.Linking;

        var work = new WorkItem(index, record.Version,record.Chunk, neighbors);
        if (!linkChannel.Writer.TryWrite(work))
            pendingLinking.Enqueue(work);
    }

    private void TryScheduleMeshing(Vec3<int> index)
    {
        if (!registry.TryGetValue(index, out var record))
            return;

        if (record.Stage != ChunkStage.Lit)
            return;

        if (!record.Flags.HasFlag(ChunkFlags.Wanted))
            return;

        if (record.Chunk is null)
            return;

        if (!TryCollectNeighbors(record.Chunk, out var neighbors))
            return;

        record.Stage = ChunkStage.Meshing;

        var work = new WorkItem(index, record.Version, record.Chunk, neighbors);
        if (!meshChannel.Writer.TryWrite(work))
            pendingMeshing.Enqueue(work);
    }

    private void HandleRelight(Vec3<int> index, ulong version)
    {
        if (!registry.TryGetValue(index, out var record)) return;
        if (record.Version != version) return;
        if (!record.Flags.HasFlag(ChunkFlags.Wanted)) return;

        switch (record.Stage)
        {
            case ChunkStage.Meshing:
                record.Version = NextVersion;
                record.Stage = ChunkStage.Lighting;
                pendingLighting.Enqueue(new WorkItem(index, record.Version, record.Chunk, record.Chunk?.Neighbors));
                return;

            case ChunkStage.Lit:
            case ChunkStage.Meshed:
                record.Stage = ChunkStage.Lighting;
                pendingLighting.Enqueue(new WorkItem(index, record.Version, record.Chunk, record.Chunk?.Neighbors));
                return;

            case ChunkStage.Lighting:
                return;

            default:
                return;
        }
    }

    private void HandleRemesh(Vec3<int> index, ulong version)
    {
        if (!registry.TryGetValue(index, out var record)) return;
        if (record.Version != version) return;
        if (!record.Flags.HasFlag(ChunkFlags.Wanted)) return;
        if (record.Chunk is null) return;

        switch (record.Stage)
        {
            case ChunkStage.Lit:
            case ChunkStage.Meshed:
                record.Stage = ChunkStage.Lit;
                TryScheduleMeshing(index);
                return;

            case ChunkStage.Meshing:
                record.Version = NextVersion;
                record.Stage = ChunkStage.Lit;
                TryScheduleMeshing(index);
                return;

            case ChunkStage.Lighting:
            case ChunkStage.Fresh:
            case ChunkStage.Generating:
            case ChunkStage.Generated:
            case ChunkStage.Linking:
                return;

            default:
                return;
        }
    }

    private void MarkForDeletion(List<Vec3<int>> toRemove)
    {
        foreach (var id in toRemove)
        {
            if (!registry.TryGetValue(id, out var record))
                continue;

            record.Flags &= ~ChunkFlags.Wanted;
            record.Flags |= ChunkFlags.DeleteRequested;
            record.Version = NextVersion;
        }
    }

    private void ProcessDeletion(List<Vec3<int>> idxDeletion)
    {
        foreach (var id in idxDeletion)
        {
            if (!registry.TryGetValue(id, out var record))
                continue;

            if (!record.Flags.HasFlag(ChunkFlags.DeleteRequested))
                continue;

            if (record.Flags.HasFlag(ChunkFlags.Wanted))
                continue;

            if (record.Stage is ChunkStage.Generating or ChunkStage.Linking
                             or ChunkStage.Lighting or ChunkStage.Meshing)
                continue;

            if (record.Chunk is not null && AnyNeighborInFlight(record.Chunk))
                continue;

            if (record.Chunk is { } chunk)
            {
                chunkMesher.Remove(chunk.Index);
                linker.UnlinkChunk(chunk);
                volume.RemoveChunk(chunk.Index);

                /*if (!volume.ContainsColumn(chunk.Index.X, chunk.Index.Z))
                    chunkGenerator.RemoveCache(chunk.Index);*/
            }

            registry.TryRemove(id, out _);
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

            if (nRecord.Stage is ChunkStage.Generating or ChunkStage.Linking
                              or ChunkStage.Lighting or ChunkStage.Meshing)
                return true;
        }
        return false;
    }

    private void Flush()
    {
        if (pendingGeneration.Count > 0)
        {
            FlushQueue(pendingGeneration, genChannel, ChunkStage.Generating);
        }
        if (pendingLinking.Count > 0)
        {
            FlushQueue(pendingLinking, linkChannel, ChunkStage.Linking);
        }
        if (pendingLighting.Count > 0)
        {
            FlushQueue(pendingLighting, lightChannel, ChunkStage.Lighting);
        }
        if (pendingMeshing.Count > 0)
        {
            FlushQueue(pendingMeshing, meshChannel, ChunkStage.Meshing);
        }
    }

    private void FlushQueue(Queue<WorkItem> pending, Channel<WorkItem> channel, ChunkStage stage)
    {
        for (int i = 0; i < ChunkCapacity; i++)
        {
            if (!pending.TryDequeue(out var item))
                break;

            if (!registry.TryGetValue(item.Index, out var record))
                continue;

            if (record.Version != item.Version)
                continue;

            if (!record.Flags.HasFlag(ChunkFlags.Wanted))
                continue;

            if (record.Stage != stage)
                continue;

            if (!channel.Writer.TryWrite(item))
                pending.Enqueue(item);
        }
    }

    private void ProcessFresh(List<Vec3<int>> toGenerate)
    {
        foreach (var id in toGenerate)
        {
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
                }
            }

            if (record.Chunk is not null)
            {
                TryScheduleLinking(id);
                TryScheduleMeshing(id);
                continue;
            }

            if (record.Stage == ChunkStage.Fresh)
            {
                var item = new WorkItem(id, record.Version, null,null);
                record.Stage = ChunkStage.Generating;

                if (!genChannel.Writer.TryWrite(item))
                    pendingGeneration.Enqueue(item);
            }
        }
    }

    private async Task GenerateChunkAsync(CancellationToken ct)
    {
        await foreach (var item in genChannel.Reader.ReadAllAsync(ct))
        {
            var result = new WorkResult(item.Index, item.Version, JobType.Generation, ResultStatus.Skipped, null, null);
            try
            {
                result.Chunk = chunkGenerator.GenerateChunk(item.Index);
                result.Status = ResultStatus.Success;
            }
            catch (Exception ex) when (ex is not (TaskCanceledException or OperationCanceledException))
            {
                Console.WriteLine(ex);
                result.Exception = ex;
                result.Status = ResultStatus.Failed;
            }

            await resultChannel.Writer.WriteAsync(result, ct);
        }
    }

    private async Task LinkChunkAsync(CancellationToken ct)
    {
        await foreach (var item in linkChannel.Reader.ReadAllAsync(ct))
        {
            var result = new WorkResult(item.Index, item.Version, JobType.Linking, ResultStatus.Skipped, item.Chunk,
                null);
            try
            {
                if (item.Chunk is null || item.Neighbors is null)
                {
                    await resultChannel.Writer.WriteAsync(result, ct);
                    continue;
                }
                
                linker.LinkChunk(item.Chunk, item.Neighbors.Value);
                result.Status = ResultStatus.Success;
            }
            catch (Exception ex) when (ex is not (TaskCanceledException or OperationCanceledException))
            {
                Console.WriteLine(ex);
                result.Exception = ex;
                result.Status = ResultStatus.Failed;
            }

            await resultChannel.Writer.WriteAsync(result, ct);
        }
    }

    private async Task LightChunkAsync(CancellationToken ct)
    {
        await foreach (var item in lightChannel.Reader.ReadAllAsync(ct))
        {
            var result = new WorkResult(item.Index, item.Version, JobType.Lighting, ResultStatus.Skipped, item.Chunk, null);
            try
            {
                if (item.Chunk is null)
                {
                    await resultChannel.Writer.WriteAsync(result, ct);
                    continue;
                }

                var chunk = item.Chunk;
                //if (chunkGenerator.IsSunlight(chunk))
                if (!chunk.IsEmpty)
                {
                    LightSystem.InitializeSkylight(chunk);
                }

                if (!chunk.IsEmpty)
                {
                    LightSystem.InitializeLight(chunk);
                }

                var (relightNeighbors, refreshMeshNeighbors) = LightSystem.RunBFS(chunk);
                
                result.Chunk = item.Chunk;
                result.Status = ResultStatus.Success;
                await resultChannel.Writer.WriteAsync(result, ct);
                // Re-feed neighbors who received new light values
                foreach (var neighbor in relightNeighbors)
                {
                    if (!registry.TryGetValue(neighbor.Index, out var neighborRecord))
                        continue;

                    var neighborRelightResult = new WorkResult(neighbor.Index, neighborRecord.Version, JobType.Lighting, ResultStatus.RelightRequested, null, null);
                    await resultChannel.Writer.WriteAsync(neighborRelightResult, ct);
                }

                // Rebuild neighbors whose boundary mesh depends on this light,
                // but who did not receive light because the boundary block is opaque
                foreach (var neighbor in refreshMeshNeighbors)
                {
                    if (relightNeighbors.Contains(neighbor))
                        continue;

                    if (!registry.TryGetValue(neighbor.Index, out var neighborRecord))
                        continue;

                    var neighborRemeshResult = new WorkResult(
                        neighbor.Index,
                        neighborRecord.Version,
                        JobType.Meshing,
                        ResultStatus.RemeshRequested,
                        null,
                        null);

                    await resultChannel.Writer.WriteAsync(neighborRemeshResult, ct);
                }
            }
            catch (Exception ex) when (ex is not (TaskCanceledException or OperationCanceledException))
            {
                Console.WriteLine(ex);
                result.Exception = ex;
                result.Status = ResultStatus.Failed;
            }

            await resultChannel.Writer.WriteAsync(result, ct);
        }
    }

    private async Task MeshChunkAsync(CancellationToken ct)
    {
        await foreach (var item in meshChannel.Reader.ReadAllAsync(ct))
        {
            var result = new WorkResult(item.Index, item.Version, JobType.Meshing, ResultStatus.Skipped, item.Chunk, null);
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
                result.Status = ResultStatus.Failed;
            }

            await resultChannel.Writer.WriteAsync(result, ct);
        }
    }

    private bool TryCollectNeighbors(Chunk chunk, out NeighborSet neighbors)
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

        return neighbors.All;
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
            await Task.WhenAll(genWorker, linkingWorker, lightingWorker, meshingWorker);
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

    private enum ResultStatus
    {
        Success,
        Failed,
        Skipped,
        RelightRequested,
        RemeshRequested
    }
}
using SharpCraft.World.Chunks;
using SharpCraft.World.Generation;
using SharpCraft.World.Meshing;
using SharpCraft.World.Lighting;
using SharpCraft.SharpMath;

using System.Numerics;
using System.Collections.Concurrent;

namespace SharpCraft.World.WorldStreaming;

internal class WorldLoader(
    ChunkVolume volume,
    ChunkGenerator chunkGenerator,
    ChunkMesher chunkMesher) : IDisposable
{
    private readonly ChunkPipeline pipeline = new(volume, chunkGenerator, chunkMesher);

    public void ScheduleForMeshing(Chunk chunk)
    {
        pipeline.AddToWorker(chunk, JobType.Meshing);
    }

    public void Recenter(Vector3 pos)
    {
        Vec3<int> center = Chunk.WorldToChunkCoords(pos);

        var indexesForGeneration = volume.CollectIndexesForGeneration(center);
        var indexesForRemoval = volume.CollectIndexesForRemoval(center);

        pipeline.Schedule(indexesForGeneration, indexesForRemoval);
    }

    public void Tick()
    {
        pipeline.Tick();
    }

    // Use to generate chunks in bulk
    public void BulkGenerate(Vector3 pos)
    {
        Vec3<int> center = Chunk.WorldToChunkCoords(pos);

        ConcurrentBag<Chunk> generatedChunks = [];
        var indexesForGeneration = volume.CollectIndexesForGeneration(center);

        var linker = new ChunkLinker();

        Parallel.ForEach(indexesForGeneration, index =>
        {
            Chunk chunk = chunkGenerator.GenerateChunk(index);
            volume.TryAdd(chunk);

            generatedChunks.Add(chunk);
            if (chunk.IsEmpty)
            {
                chunk.IsReady = true;
            }
        });

        foreach (var chunk in generatedChunks)
        {
            var neighbors = volume.CollectNeighbors(chunk);
            linker.LinkChunk(chunk, neighbors);
        }

        List<Chunk> readyChunks = [];
        List<Chunk> sunlightChunks = [];
        foreach (var chunk in generatedChunks)
        {
            if (chunkGenerator.IsSunlight(chunk))
            {
                sunlightChunks.Add(chunk);
            }

            if (chunk.IsReady)
            {
                pipeline.AddToRegistry(chunk, ChunkStage.Meshed);
                continue;
            }

            if (chunk.Neighbors.All)
            {
                readyChunks.Add(chunk);
            }
            else
            {
                pipeline.AddToRegistry(chunk, ChunkStage.Generated);
            }
        }

        Parallel.ForEach(sunlightChunks, chunk =>
        {
            var neighbors = chunk.Neighbors;

            LightSystem.InitializeSkylight(chunk);
            var (_, spilledLight) = LightSystem.RunBFS(chunk, chunk.Neighbors);

            if (spilledLight.ZPos.Count != 0) foreach (var node in spilledLight.ZPos) neighbors.ZPos!.EnqueueLight(node);
            if (spilledLight.ZNeg.Count != 0) foreach (var node in spilledLight.ZNeg) neighbors.ZNeg!.EnqueueLight(node);
            if (spilledLight.XPos.Count != 0) foreach (var node in spilledLight.XPos) neighbors.XPos!.EnqueueLight(node);
            if (spilledLight.XNeg.Count != 0) foreach (var node in spilledLight.XNeg) neighbors.XNeg!.EnqueueLight(node);
            if (spilledLight.YPos.Count != 0) foreach (var node in spilledLight.YPos) neighbors.YPos!.EnqueueLight(node);
            if (spilledLight.YNeg.Count != 0) foreach (var node in spilledLight.YNeg) neighbors.YNeg!.EnqueueLight(node);
        });

        Parallel.ForEach(readyChunks, chunk =>
        {
            var neighbors = chunk.Neighbors;

            LightSystem.InitializeLight(chunk);
            var (_, spilledLight) = LightSystem.RunBFS(chunk, chunk.Neighbors);

            if (spilledLight.ZPos.Count != 0) foreach (var node in spilledLight.ZPos) neighbors.ZPos!.EnqueueLight(node);
            if (spilledLight.ZNeg.Count != 0) foreach (var node in spilledLight.ZNeg) neighbors.ZNeg!.EnqueueLight(node);
            if (spilledLight.XPos.Count != 0) foreach (var node in spilledLight.XPos) neighbors.XPos!.EnqueueLight(node);
            if (spilledLight.XNeg.Count != 0) foreach (var node in spilledLight.XNeg) neighbors.XNeg!.EnqueueLight(node);
            if (spilledLight.YPos.Count != 0) foreach (var node in spilledLight.YPos) neighbors.YPos!.EnqueueLight(node);
            if (spilledLight.YNeg.Count != 0) foreach (var node in spilledLight.YNeg) neighbors.YNeg!.EnqueueLight(node);
        });

        int anyPending;
        do
        {
            anyPending = 0;

            Parallel.ForEach(readyChunks, chunk =>
            {
                if (!chunk.LightQueue.IsEmpty)
                {
                    LightSystem.RunBFS(chunk, chunk.Neighbors);
                    Interlocked.Exchange(ref anyPending, 1);
                }
            });

        } while (anyPending != 0);

        Parallel.ForEach(readyChunks, chunk =>
        {
            chunkMesher.Build(chunk, chunk.Neighbors);
            chunk.IsReady = true;
            pipeline.AddToRegistry(chunk, ChunkStage.Meshed);
        });
    }

    //public ReliefType GetReliefType(int cx, int cz, int bx, int bz) => chunkGenerator.GetReliefType(cx, cz, bx, bz);

    // Disposal
    private bool disposed;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposed) return;
        disposed = true;

        if (disposing)
        {
            pipeline.Dispose();
        }
    }
}
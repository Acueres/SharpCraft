using SharpCraft.World.Chunks;
using SharpCraft.World.Generation;
using SharpCraft.World.Meshing;
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

        var linker = new ChunkLinker(volume);

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
            linker.LinkChunk(chunk);
        }

        List<Chunk> readyChunks = [];
        List<Chunk> sunlightChunks = [];
        foreach (var chunk in generatedChunks)
        {
            /*if (chunkGenerator.IsSunlight(chunk))
            {
                sunlightChunks.Add(chunk);
            }*/

            if (chunk.IsReady)
            {
                pipeline.AddToRegistry(chunk, ChunkStage.Meshed);
                continue;
            }

            if (chunk.AllNeighborsExist)
            {
                readyChunks.Add(chunk);
            }
            else
            {
                pipeline.AddToRegistry(chunk, ChunkStage.Generated);
            }
        }

        /*Parallel.ForEach(sunlightChunks, chunk =>
        {
            LightSystem.InitializeSkylight(chunk);
            LightSystem.RunBFS(chunk);
        });

        Parallel.ForEach(readyChunks, chunk =>
        {
            LightSystem.InitializeLight(chunk);
            LightSystem.RunBFS(chunk);
        });*/

        /*int anyPending;
        do
        {
            anyPending = 0;

            Parallel.ForEach(readyChunks, chunk =>
            {
                if (!chunk.LightQueue.IsEmpty)
                {
                    LightSystem.RunBFS(chunk);
                    Interlocked.Exchange(ref anyPending, 1);
                }
            });

        } while (anyPending != 0);*/

        Parallel.ForEach(readyChunks, chunk =>
        {
            chunkMesher.Build(chunk);
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

    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;
        disposed = true;

        if (disposing)
        {
            pipeline.Dispose();
        }
    }
}
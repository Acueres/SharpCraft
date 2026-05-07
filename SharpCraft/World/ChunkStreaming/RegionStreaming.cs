using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Xna.Framework;

using SharpCraft.World.Lighting;
using SharpCraft.World.Chunks;
using SharpCraft.MathUtilities;
using SharpCraft.World.Meshing;
using SharpCraft.World.Generation;

namespace SharpCraft.World.ChunkStreaming;

class RegionStreaming(
    Region region,
    ChunkGenerator chunkGenerator,
    LightSystem lightSystem,
    ChunkMesher chunkMesher) : IDisposable
{
    readonly ChunkDispatcher dispatcher = new(region, chunkGenerator, lightSystem, chunkMesher);

    public void ScheduleForMeshing(Chunk chunk)
    {
        dispatcher.AddToWorker(chunk, JobType.Meshing);
    }

    public void Update(Vector3 pos)
    {
        Vec3<int> center = Chunk.WorldToChunkCoords(pos);

        var indexesForGeneration = region.CollectIndexesForGeneration(center);
        var indexesForRemoval = region.CollectIndexesForRemoval(center);

        dispatcher.Dispatch(indexesForGeneration, indexesForRemoval);
    }

    public void Update()
    {
        dispatcher.Update();
    }

    // Use to generate chunks in bulk
    public void BulkGenerate(Vector3 pos)
    {
        Vec3<int> center = Chunk.WorldToChunkCoords(pos);

        ConcurrentBag<Chunk> generatedChunks = [];
        var indexesForGeneration = region.CollectIndexesForGeneration(center);

        Parallel.ForEach(indexesForGeneration, index =>
        {
            Chunk chunk = chunkGenerator.GenerateChunk(index);
            region[index] = chunk;

            generatedChunks.Add(chunk);
            if (chunk.IsEmpty)
            {
                chunk.State = ChunkState.Ready;
            }
            else
            {
                chunk.State = ChunkState.Generated;
            }
        });

        foreach (var chunk in generatedChunks)
        {
            region.LinkChunk(chunk);
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
                dispatcher.AddToRegistry(chunk, ChunkStage.Meshed);
                continue;
            }

            if (chunk.AllNeighborsExist)
            {
                readyChunks.Add(chunk);
            }
            else
            {
                dispatcher.AddToRegistry(chunk, ChunkStage.Generated);
            }
        }

        Parallel.ForEach(sunlightChunks, chunk =>
        {
            LightSystem.InitializeSkylight(chunk);
            LightSystem.RunBFS(chunk);
        });

        Parallel.ForEach(readyChunks, chunk =>
        {
            LightSystem.InitializeLight(chunk);
            LightSystem.RunBFS(chunk);
        });

        int anyPending;
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

        } while (anyPending != 0);

        Parallel.ForEach(readyChunks, chunk =>
        {
            chunkMesher.Build(chunk);
            chunk.State = ChunkState.Ready;
            dispatcher.AddToRegistry(chunk, ChunkStage.Meshed);
        });
    }

    public ReliefType GetReliefType(int cx, int cz, int bx, int bz) => chunkGenerator.GetReliefType(cx, cz, bx, bz);

    // Disposal
    bool disposed;

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
            dispatcher.Dispose();
        }
    }
}

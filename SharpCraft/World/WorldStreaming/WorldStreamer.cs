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

namespace SharpCraft.World.WorldStreaming;

class WorldStreamer(
    Region region,
    ChunkGenerator chunkGenerator,
    ChunkMesher chunkMesher) : IDisposable
{
    readonly ChunkScheduler scheduler = new(region, chunkGenerator, chunkMesher);

    public void ScheduleForMeshing(Chunk chunk)
    {
        scheduler.AddToWorker(chunk, JobType.Meshing);
    }

    public void Recenter(Vector3 pos)
    {
        Vec3<int> center = Chunk.WorldToChunkCoords(pos);

        var indexesForGeneration = region.CollectIndexesForGeneration(center);
        var indexesForRemoval = region.CollectIndexesForRemoval(center);

        scheduler.Schedule(indexesForGeneration, indexesForRemoval);
    }

    public void Tick()
    {
        scheduler.Tick();
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
                chunk.IsReady = true;
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
                scheduler.AddToRegistry(chunk, ChunkStage.Meshed);
                continue;
            }

            if (chunk.AllNeighborsExist)
            {
                readyChunks.Add(chunk);
            }
            else
            {
                scheduler.AddToRegistry(chunk, ChunkStage.Generated);
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
            chunk.IsReady = true;
            scheduler.AddToRegistry(chunk, ChunkStage.Meshed);
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
            scheduler.Dispose();
        }
    }
}

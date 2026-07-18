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

    public void Recenter(Vector3 pos)
    {
        Vec3<int> center = Chunk.WorldToChunkCoords(pos);

        volume.SetCenter(center);
        var indexesForGeneration = volume.CollectIndexesForGeneration();
        var indexesForRemoval = volume.CollectIndexesForRemoval();

        pipeline.Schedule(indexesForGeneration, indexesForRemoval);
    }

    public bool Tick()
    {
        return pipeline.Tick();
    }

    // Use to generate chunks in bulk
    public void BulkGenerate(Vector3 pos)
    {
        Vec3<int> center = Chunk.WorldToChunkCoords(pos);
        
        volume.SetCenter(center);

        ConcurrentBag<Chunk> generatedChunks = [];
        var indexesForGeneration = volume.CollectIndexesForGeneration();
        
        Parallel.ForEach(indexesForGeneration, index =>
        {
            Chunk chunk = chunkGenerator.GenerateChunk(index);
            volume.Add(chunk);

            generatedChunks.Add(chunk);
            if (chunk.IsEmpty)
            {
                chunk.IsReady = true;
            }
        });
        
        Dictionary<Vec3<int>, NeighborSet> neighbors = [];
        foreach (var chunk in generatedChunks)
        {
            var n = volume.CollectNeighbors(chunk);
            neighbors.Add(chunk.Index, n);
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
            
            var n = neighbors[chunk.Index];
            if (n.All)
            {
                readyChunks.Add(chunk);
            }
            else
            {
                pipeline.AddToRegistry(chunk, ChunkStage.Generated);
            }
        }

        var bulkLight = new BulkLight(neighbors);

        foreach (var chunk in sunlightChunks)
        {
            bulkLight.SeedSkylight(chunk);
        }

        foreach (var chunk in readyChunks)
        {
            bulkLight.SeedBlockLights(chunk);
        }
        
        bulkLight.Flood();

        Parallel.ForEach(readyChunks, chunk =>
        {
            var n = neighbors[chunk.Index];
            
            chunkMesher.Build(chunk, n);
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
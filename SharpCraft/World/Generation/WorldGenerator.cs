using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Xna.Framework;

using SharpCraft.World.Lighting;
using SharpCraft.World.Chunks;
using SharpCraft.MathUtilities;
using SharpCraft.Rendering.Meshers;

namespace SharpCraft.World.Generation;

class WorldGenerator : IDisposable
{
    readonly Region region;
    readonly ChunkGenerator chunkGenerator;
    readonly LightSystem lightSystem;
    readonly ChunkMesher chunkMesher;

    readonly CancellationTokenSource cts = new();

    // Generated chunks still awaiting 6 neighbors
    readonly ConcurrentDictionary<Vec3<int>, Chunk> pendingLinking = [];

    // 1. Accepts indices to generate chunks from
    readonly TransformBlock<Vec3<int>, Chunk> generateBlock;
    // 2. Link neighbours, resolve chunks pending for linking
    readonly ActionBlock<Chunk> linkingBlock;
    // 3. Seed skylight / blocklight
    readonly TransformBlock<Chunk, Chunk> lightSeedBlock;
    // 4. Single-threaded light queue BFS
    readonly ActionBlock<Chunk> floodFillBlock;
    // 5. Meshing
    readonly ActionBlock<Chunk> meshBlock;
    // Chunk deletion
    readonly ActionBlock<Chunk> deletionBlock;

    public WorldGenerator(
        Region region,
        ChunkGenerator chunkGenerator,
        LightSystem lightSystem,
        ChunkMesher chunkMesher,
        int maxWorkers)
    {
        this.region = region;
        this.chunkGenerator = chunkGenerator;
        this.lightSystem = lightSystem;
        this.chunkMesher = chunkMesher;

        // 1. Generation
        generateBlock = new TransformBlock<Vec3<int>, Chunk>(
        idx =>
            {
                try
                {
                    var chunk = chunkGenerator.GenerateChunk(idx);
                    region[idx] = chunk;
                    chunk.State = ChunkState.Generated;
                    return chunk;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxWorkers,
                CancellationToken = cts.Token
            });

        // 2. Neighbors linking
        linkingBlock = new ActionBlock<Chunk>(
            chunk =>
            {
                try
                {
                    region.LinkChunk(chunk);

                    if (chunk.AllNeighborsExist)
                    {
                        pendingLinking.TryRemove(chunk.Index, out _);
                        chunk.State = ChunkState.Linked;
                        lightSeedBlock.Post(chunk);
                    }
                    else
                    {
                        pendingLinking[chunk.Index] = chunk;
                    }

                    foreach (var n in chunk.GetNeighbours())
                    {
                        if (n.AllNeighborsExist && n.State == ChunkState.Generated) linkingBlock.Post(n);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxWorkers,
                CancellationToken = cts.Token
            });

        // 3. Seed light
        lightSeedBlock = new TransformBlock<Chunk, Chunk>(
            chunk =>
            {
                try
                {
                    if (chunkGenerator.IsSunlight(chunk))
                    {
                        lightSystem.InitializeSkylight(chunk);
                    }
                    else if (!chunk.IsEmpty)
                    {
                        lightSystem.InitializeLight(chunk);
                        chunk.State = ChunkState.LightSeeded;
                    }

                    return chunk;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxWorkers,
                CancellationToken = cts.Token
            });

        // 4. Flood fill
        // One global single‑threaded queue
        floodFillBlock = new ActionBlock<Chunk>(
            chunk =>
            {
                try
                {
                    var visitedChunks = lightSystem.Run();
                    visitedChunks.Remove(chunk); // avoid sending origin chunk twice into the meshing stage
                    foreach (var visitedChunk in visitedChunks)
                    {
                        // remesh chunks whose light values have been modified in this run
                        if (!visitedChunk.IsEmpty)
                        {
                            meshBlock.Post(visitedChunk);
                        }
                    }

                    chunk.State = ChunkState.Lit;
                    meshBlock.Post(chunk);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            },
            new ExecutionDataflowBlockOptions
            {
                // run on a single thread to ensure BFS queue safety
                MaxDegreeOfParallelism = 1,
                CancellationToken = cts.Token
            });

        // 5. Meshing
        meshBlock = new ActionBlock<Chunk>(
            chunk =>
            {
                if (!chunk.IsEmpty)
                {
                    chunkMesher.AddMesh(chunk);
                }
                chunk.State = ChunkState.Ready;
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxWorkers,
                CancellationToken = cts.Token
            });

        // Wire the pipeline
        generateBlock.LinkTo(linkingBlock, new DataflowLinkOptions { PropagateCompletion = true });
        lightSeedBlock.LinkTo(floodFillBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // Set up the chunk deleting system
        deletionBlock = new ActionBlock<Chunk>(
        chunk =>
        {
            chunkMesher.Remove(chunk.Index);
            region.RemoveChunk(chunk.Index);

            if (!region.ContainsIndex(chunk.Index.X, chunk.Index.Z))
            {
                chunkGenerator.RemoveCache(chunk.Index);
            }
        });
    }

    public void Update(Vector3 pos)
    {
        Vec3<int> center = Chunk.WorldToChunkCoords(pos);

        var indexesForGeneration = region.CollectIndexesForGeneration(center);
        foreach (var index in indexesForGeneration)
        {
            generateBlock.Post(index);
        }

        var indexesForRemoval = region.CollectIndexesForRemoval(center);
        foreach (var index in indexesForRemoval)
        {
            pendingLinking.TryRemove(index, out _);
            var chunk = region[index];
            if (chunk != null && chunk.IsReady)
            {
                chunk.State = ChunkState.Unloaded;
                deletionBlock.Post(chunk);
            }
        }
    }

    // Used to generate chunks in bulk off-screen
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

            if (chunk.IsReady) continue;

            if (!chunk.AllNeighborsExist)
            {
                pendingLinking.TryAdd(chunk.Index, chunk);
                continue;
            }
            else
            {
                readyChunks.Add(chunk);
            }
        }

        Parallel.ForEach(sunlightChunks, chunk =>
        {
            lightSystem.InitializeSkylight(chunk);
        });

        Parallel.ForEach(readyChunks, chunk =>
        {
            lightSystem.InitializeLight(chunk);
        });

        lightSystem.Run();

        Parallel.ForEach(readyChunks, chunk =>
        {
            chunkMesher.AddMesh(chunk);
            chunk.State = ChunkState.Ready;
        });
    }

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
            generateBlock.Complete();
            meshBlock.Complete();
            deletionBlock.Complete();

            cts.Cancel();
            cts.Dispose();
        }
    }
}

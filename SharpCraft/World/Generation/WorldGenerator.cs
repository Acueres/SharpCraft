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

    // 1. Accepts indices to generate chunks from
    readonly TransformBlock<Vec3<int>, Chunk> generateBlock;

    // 2. Link neighbours, resolve chunks pending for linking
    readonly TransformManyBlock<Chunk, Chunk> linkingBlock;
    // Handle re-posting to the linking phase
    readonly BufferBlock<Chunk> linkingInputBuffer;

    // 3. Seed skylight / blocklight
    readonly TransformBlock<Chunk, Chunk> lightSeedBlock;

    // 4. Single-threaded light queue BFS
    readonly TransformManyBlock<Chunk, Chunk> floodFillBlock;

    // 5. Meshing
    readonly ActionBlock<Chunk> meshingBlock;
    // Handle re-posting to the meshing phase
    readonly BufferBlock<Chunk> meshingInputBuffer;

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
        linkingInputBuffer = new BufferBlock<Chunk>(
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxWorkers,
                CancellationToken = cts.Token
            });

        linkingBlock = new TransformManyBlock<Chunk, Chunk>(
            chunk =>
            {
                try
                {
                    List<Chunk> ready = new(1);
                    region.LinkChunk(chunk);

                    if (chunk.AllNeighborsExist)
                    {
                        chunk.State = ChunkState.Linked;
                        ready.Add(chunk);
                    }

                    foreach (var n in chunk.GetNeighbours())
                    {
                        if (n.State == ChunkState.Generated && n.AllNeighborsExist)
                        {
                            linkingInputBuffer.Post(n);
                        }
                    }

                    return ready;
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
        floodFillBlock = new TransformManyBlock<Chunk, Chunk>(
            chunk =>
            {
                try
                {
                    List<Chunk> ready = [];

                    var visitedChunks = lightSystem.RunBFS();
                    visitedChunks.Remove(chunk);

                    chunk.State = ChunkState.Lit;
                    ready.Add(chunk);

                    foreach (var visitedChunk in visitedChunks)
                    {
                        // For each 'visitedChunk' (whose lighting was just re-calculated):
                        // If it's a meshable candidate (not empty, all neighbors exist), queue it for the mesher if either:
                        //  1. It is ready (was previously meshed): its state is updated to 'Lit' to trigger a mesh rebuild reflecting the new lighting.
                        //  2. Its current state is 'Lit' (already pending meshing or re-lit): it's added to the queue.
                        // This ensures chunks with lighting changes are (re)processed by the meshing stage.
                        if (!visitedChunk.IsEmpty && visitedChunk.AllNeighborsExist
                        && (chunk.IsReady || chunk.State == ChunkState.Lit))
                        {
                            visitedChunk.State = ChunkState.Lit;
                            ready.Add(visitedChunk);
                        }
                    }

                    return ready;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            },
            new ExecutionDataflowBlockOptions
            {
                // Run on a single thread to ensure BFS queue safety
                MaxDegreeOfParallelism = 1,
                CancellationToken = cts.Token
            });

        // 5. Meshing
        meshingInputBuffer = new BufferBlock<Chunk>(
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxWorkers,
                CancellationToken = cts.Token
            });

        meshingBlock = new ActionBlock<Chunk>(
            chunk =>
            {
                try
                {
                    chunkMesher.Build(chunk);
                    chunk.State = ChunkState.Ready;
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

        // Wire the pipeline
        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

        generateBlock.LinkTo(linkingInputBuffer, linkOptions);
        linkingInputBuffer.LinkTo(linkingBlock, linkOptions);
        linkingBlock.LinkTo(lightSeedBlock, linkOptions);
        lightSeedBlock.LinkTo(floodFillBlock, linkOptions);
        floodFillBlock.LinkTo(meshingInputBuffer, linkOptions);
        meshingInputBuffer.LinkTo(meshingBlock, linkOptions);

        // Set up the chunk deletion system
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

    public void PostToMesher(Chunk chunk)
    {
        meshingBlock.Post(chunk);
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

            if (chunk.AllNeighborsExist)
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

        lightSystem.RunBFS();

        Parallel.ForEach(readyChunks, chunk =>
        {
            chunkMesher.Build(chunk);
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
            meshingBlock.Complete();
            deletionBlock.Complete();

            cts.Cancel();
            cts.Dispose();
        }
    }
}

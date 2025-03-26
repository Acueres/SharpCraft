using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Xna.Framework;

using SharpCraft.World.Lighting;
using SharpCraft.World.Chunks;
using SharpCraft.World.Generation;
using SharpCraft.MathUtilities;
using SharpCraft.Rendering.Meshers;

namespace SharpCraft.World
{
    class Region
    {
        //Number of chunks from the center chunk to the edge of the chunk area
        readonly int apothem;

        readonly WorldGenerator worldGenerator;
        readonly LightSystem lightSystem;
        readonly AdjacencyGraph adjacencyGraph;
        readonly ChunkMesher chunkMesher;

        readonly ConcurrentDictionary<Vec3<int>, Chunk> chunks = [];
        readonly List<Vec3<int>> proximityIndexes = [];
        readonly ConcurrentBag<Vec3<int>> activeChunkIndexes = [];
        readonly HashSet<Vec3<int>> inactiveChunkIndexes = [];
        readonly HashSet<Vec3<int>> unfinishedChunkIndexes = [];

        public Region(int apothem, AdjacencyGraph adjacencyGraph, WorldGenerator worldGenerator, LightSystem lightSystem, ChunkMesher chunkMesher)
        {
            this.apothem = apothem;
            this.worldGenerator = worldGenerator;

            proximityIndexes = GenerateProximityIndexes();
            this.lightSystem = lightSystem;
            this.adjacencyGraph = adjacencyGraph;
            this.chunkMesher = chunkMesher;
        }

        public Chunk GetChunk(Vec3<int> index)
        {
            return chunks[index];
        }

        public void Update(Vector3 pos)
        {
            Vec3<int> center = Chunk.WorldToChunkCoords(pos);

            inactiveChunkIndexes.UnionWith(activeChunkIndexes);

            GenerateChunks(center);
            RemoveInactiveChunks();
        }

        public void UpdateMeshes()
        {
            foreach (Vec3<int> index in activeChunkIndexes)
            {
                Chunk chunk = GetChunk(index);
                if (chunk.IsEmpty) continue;

                if (chunk.RecalculateMesh)
                {
                    var adjacency = adjacencyGraph.GetAdjacency(index);

                    if (adjacency.All())
                    {
                        chunk.IsReady = true;
                        chunk.GenerateIndexCaches(adjacency);
                        chunkMesher.CreateMesh(adjacency);
                    }
                }
            }
        }

        public IEnumerable<Chunk> GetActiveChunks()
        {
            foreach (Vec3<int> index in activeChunkIndexes) { yield return chunks[index]; }
        }

        List<Vec3<int>> GenerateProximityIndexes()
        {
            List<Vec3<int>> indexes = [];
            for (int x = -apothem; x <= apothem; x++)
            {
                for (int y = -apothem; y <= apothem; y++)
                {
                    for (int z = -apothem; z <= apothem; z++)
                    {
                        Vec3<int> index = new(x, y, z);
                        indexes.Add(index);
                    }
                }
            }

            indexes = [.. indexes.OrderBy(index => Math.Abs(index.X) + Math.Abs(index.Y) + Math.Abs(index.Z))];

            return indexes;
        }

        void GenerateChunks(Vec3<int> center)
        {
            activeChunkIndexes.Clear();
            ConcurrentBag<Vec3<int>> generatedChunks = [];
            ConcurrentDictionary<Vec3<int>, ChunkBuffer> blockBufferCache = [];

            Parallel.ForEach(proximityIndexes, proximityIndex =>
            {
                Vec3<int> index = center + proximityIndex;

                if (chunks.ContainsKey(index))
                {
                    lock (unfinishedChunkIndexes)
                    {
                        if (unfinishedChunkIndexes.Remove(index))
                            generatedChunks.Add(index);
                    }
                }
                else
                {
                    (Chunk chunk, ChunkBuffer blocks) = worldGenerator.GenerateChunk(index);
                    chunks.TryAdd(index, chunk);
                    blockBufferCache.TryAdd(index, blocks);
                    generatedChunks.Add(index);
                }

                activeChunkIndexes.Add(index);

                lock (inactiveChunkIndexes)
                    inactiveChunkIndexes.Remove(index);
            });

            foreach (Vec3<int> index in generatedChunks)
            {
                Chunk chunk = chunks[index];
                adjacencyGraph.CalculateChunkAdjacency(chunk);
            }

            List<ChunkAdjacency> readyChunks = [];

            foreach (Vec3<int> index in generatedChunks)
            {
                ChunkAdjacency adjacency = adjacencyGraph.GetAdjacency(index);

                if (adjacency.Root.IsEmpty) continue;

                adjacency.Root.IsReady = adjacency.All();

                if (adjacency.Root.IsReady)
                {
                    adjacency.Root.InitLight();
                    if (blockBufferCache.TryGetValue(adjacency.Root.Index, out var buffer))
                    {
                        adjacency.Root.GenerateIndexCaches(buffer, adjacency);
                    }
                    else
                    {
                        adjacency.Root.GenerateIndexCaches(adjacency);
                    }

                    readyChunks.Add(adjacency);
                }
                else
                {
                    unfinishedChunkIndexes.Add(index);
                }
            }

            List<ChunkAdjacency> skyChunks = [.. worldGenerator.GetSkyLevel().Select(adjacencyGraph.GetAdjacency).Where(x => x is not null && x.All())];

            foreach (ChunkAdjacency adjacency in skyChunks)
            {
                lightSystem.InitializeSkylight(adjacency);
            }

            foreach (ChunkAdjacency adjacency in readyChunks)
            {
                lightSystem.InitializeLight(adjacency);
            }

            lightSystem.FloodFill();

            foreach (ChunkAdjacency adjacency in readyChunks)
            {
                if (blockBufferCache.TryGetValue(adjacency.Root.Index, out var buffer))
                {
                    chunkMesher.CreateMesh(adjacency, buffer);
                }
                else
                {
                    chunkMesher.CreateMesh(adjacency);
                }
            }

            worldGenerator.ClearCache();
        }

        void RemoveInactiveChunks()
        {
            foreach (Vec3<int> index in inactiveChunkIndexes)
            {
                adjacencyGraph.Dereference(index);
                chunks[index].Dispose();
                chunks.Remove(index, out _);
                chunkMesher.Remove(index);
            }
            inactiveChunkIndexes.Clear();
        }
    }
}

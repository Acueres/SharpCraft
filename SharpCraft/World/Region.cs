using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Xna.Framework;

using SharpCraft.Utilities;
using SharpCraft.World.Light;
using SharpCraft.World.Chunks;
using SharpCraft.World.Generation;
using SharpCraft.MathUtilities;

namespace SharpCraft.World
{
    class Region
    {
        //Number of chunks from the center chunk to the edge of the chunk area
        readonly int apothem;

        readonly WorldGenerator worldGenerator;
        readonly RegionRenderer renderer;
        readonly LightSystem lightSystem;
        readonly AdjacencyGraph adjacencyGraph;

        readonly ConcurrentDictionary<Vector3I, IChunk> chunks = [];
        readonly List<Vector3I> proximityIndexes = [];
        readonly ConcurrentBag<Vector3I> activeChunkIndexes = [];
        readonly HashSet<Vector3I> inactiveChunkIndexes = [];
        readonly HashSet<Vector3I> unfinishedChunkIndexes = [];

        public Region(int apothem, AdjacencyGraph adjacencyGraph, WorldGenerator worldGenerator, RegionRenderer renderer, LightSystem lightSystem)
        {
            this.apothem = apothem;
            this.worldGenerator = worldGenerator;
            this.renderer = renderer;

            proximityIndexes = GenerateProximityIndexes();
            this.lightSystem = lightSystem;
            this.adjacencyGraph = adjacencyGraph;
        }

        public IChunk GetChunk(Vector3I index)
        {
            return chunks[index];
        }

        public void Update(Vector3 pos)
        {
            Vector3I center = Chunk.WorldToChunkCoords(pos);

            inactiveChunkIndexes.UnionWith(activeChunkIndexes);

            GenerateChunks(center);
            RemoveInactiveChunks();
        }

        public void UpdateMeshes()
        {
            foreach (Vector3I index in activeChunkIndexes)
            {
                IChunk chunk = GetChunk(index);
                if (chunk.RecalculateMesh)
                {
                    var adjacency = adjacencyGraph.GetAdjacency(index);

                    if (adjacency.All())
                    {
                        renderer.Update(adjacency);
                    }
                }
            }
        }

        public void Render(Player player, Time time)
        {
            renderer.Render(GetActiveChunks(), player, time);
        }

        public static HashSet<Vector3I> GetReachableChunkIndexes(Vector3 pos)
        {
            int xIndex = Chunk.WorldToChunkIndex(pos.X);
            int xIndexPlus6 = Chunk.WorldToChunkIndex(pos.X + 6);
            int xIndexMinus6 = Chunk.WorldToChunkIndex(pos.X - 6);
            Span<int> xValues = [xIndex, xIndexPlus6, xIndexMinus6];

            int yIndex = Chunk.WorldToChunkIndex(pos.Y);
            int yIndexPlus6 = Chunk.WorldToChunkIndex(pos.Y + 6);
            int yIndexMinus6 = Chunk.WorldToChunkIndex(pos.Y - 6);
            Span<int> yValues = [yIndex, yIndexPlus6, yIndexMinus6];

            int zIndex = Chunk.WorldToChunkIndex(pos.Z);
            int zIndexPlus6 = Chunk.WorldToChunkIndex(pos.Z + 6);
            int zIndexMinus6 = Chunk.WorldToChunkIndex(pos.Z - 6);
            Span<int> zValues = [zIndex, zIndexPlus6, zIndexMinus6];

            HashSet<Vector3I> indexes = [];
            foreach (int x in xValues)
            {
                foreach (int y in yValues)
                {
                    foreach(int z in zValues)
                    {
                        indexes.Add(new(x, y, z));
                    }
                }
            }

            return indexes;
        }

        public IEnumerable<IChunk> GetActiveChunks()
        {
            foreach (Vector3I index in activeChunkIndexes) { yield return chunks[index]; }
        }

        List<Vector3I> GenerateProximityIndexes()
        {
            List<Vector3I> indexes = [];
            for (int x = -apothem; x <= apothem; x++)
            {
                for (int y = -apothem; y <= apothem; y++)
                {
                    for (int z = -apothem; z <= apothem; z++)
                    {
                        Vector3I index = new(x, y, z);
                        indexes.Add(index);
                    }
                }
            }

            indexes = [.. indexes.OrderBy(index => Math.Abs(index.X) + Math.Abs(index.Y) + Math.Abs(index.Z))];

            return indexes;
        }

        void GenerateChunks(Vector3I center)
        {
            activeChunkIndexes.Clear();
            ConcurrentBag<Vector3I> generatedChunks = [];

            Parallel.ForEach(proximityIndexes, proximityIndex =>
            {
                Vector3I index = center + proximityIndex;

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
                    IChunk chunk = worldGenerator.GenerateChunk(index);
                    chunks.TryAdd(index, chunk);
                    generatedChunks.Add(index);
                }

                activeChunkIndexes.Add(index);

                lock (inactiveChunkIndexes)
                    inactiveChunkIndexes.Remove(index);
            });

            foreach (Vector3I index in generatedChunks)
            {
                IChunk chunk = chunks[index];
                adjacencyGraph.CalculateChunkAdjacency(chunk);
            }

            List<ChunkAdjacency> readyChunks = [];

            foreach (Vector3I index in generatedChunks)
            {
                ChunkAdjacency adjacency = adjacencyGraph.GetAdjacency(index);
                adjacency.Root.IsReady = adjacency.All();

                if (adjacency.Root.IsReady)
                {
                    adjacency.Root.CalculateActiveBlocks(adjacency);
                    if (adjacency.Root is not SkyChunk) readyChunks.Add(adjacency);
                }
                else
                {
                    unfinishedChunkIndexes.Add(index);
                }
            }

            List<ChunkAdjacency> skyChunks = [.. worldGenerator.GetSkyLevel().Select(adjacencyGraph.GetAdjacency).Where(x => x is not null && x.All())];

            foreach (ChunkAdjacency n in skyChunks)
            {
                lightSystem.InitializeSkylight(n);
            }

            foreach (ChunkAdjacency n in readyChunks)
            {
                lightSystem.InitializeLight(n);
            }

            lightSystem.FloodFill();

            foreach (ChunkAdjacency n in readyChunks)
            {
                renderer.AddMesh(n);
            }

            worldGenerator.ClearCache();
        }

        void RemoveInactiveChunks()
        {
            foreach (Vector3I index in inactiveChunkIndexes)
            {
                adjacencyGraph.Dereference(index);
                chunks[index].Dispose();
                chunks.Remove(index, out _);
                renderer.Remove(index);
            }
            inactiveChunkIndexes.Clear();
        }
    }
}

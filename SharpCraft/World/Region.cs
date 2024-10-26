using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using SharpCraft.Handlers;
using SharpCraft.Utility;

namespace SharpCraft.World
{
    public class ChunkNeighbors
    {
        public Chunk Chunk {  get; set; }
        public Chunk XNeg { get; set; }
        public Chunk XPos {  get; set; }
        public Chunk YPos { get; set; }
        public Chunk YNeg { get; set; }
        public Chunk ZNeg { get; set; }
        public Chunk ZPos { get; set; }

        public bool AllExist()
        {
            return XNeg is not null && XPos is not null && ZNeg is not null && ZPos is not null
                && YNeg is not null && YPos is not null;
        }
    }

    class Region
    {
        //Number of chunks from the center chunk to the edge of the chunk area
        readonly int apothem;

        readonly WorldGenerator worldGenerator;
        readonly DatabaseHandler databaseHandler;
        readonly RegionRenderer renderer;

        readonly Dictionary<Vector3I, Chunk> chunks = [];
        readonly Dictionary<Vector3I, ChunkNeighbors> neighborsMap = [];
        readonly List<Vector3I> proximityIndexes = [];
        readonly List<Vector3I> activeChunkIndexes = [];
        readonly HashSet<Vector3I> inactiveChunkIndexes = [];
        readonly HashSet<Vector3I> unfinishedChunkIndexes = [];

        public Region(int apothem, WorldGenerator worldGenerator, DatabaseHandler databaseHandler, RegionRenderer renderer)
        {
            this.apothem = apothem;
            this.worldGenerator = worldGenerator;
            this.databaseHandler = databaseHandler;
            this.renderer = renderer;

            proximityIndexes = GenerateProximityIndexes();
        }

        public Chunk GetChunk(Vector3I index)
        {
            return chunks[index];
        }

        public void Update(Vector3 pos)
        {
            int x = Chunk.CalculateChunkIndex(pos.X);
            int y = Chunk.CalculateChunkIndex(pos.Y);
            int z = Chunk.CalculateChunkIndex(pos.Z);

            Vector3I center = new(x, y, z);

            inactiveChunkIndexes.UnionWith(activeChunkIndexes);

            GenerateChunks(center);
            RemoveInactiveChunks();

            foreach (Vector3I index in activeChunkIndexes)
            {
                Chunk chunk = GetChunk(index);
                if (chunk.RecalculateMesh)
                {
                    var neighbors = GetChunkNeighbors(index);

                    if (neighbors.AllExist())
                    {
                        renderer.Update(neighbors);
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
            int xIndex = Chunk.CalculateChunkIndex(pos.X);
            int xIndexPlus6 = Chunk.CalculateChunkIndex(pos.X + 6);
            int xIndexMinus6 = Chunk.CalculateChunkIndex(pos.X - 6);
            Span<int> xValues = [xIndex, xIndexPlus6, xIndexMinus6];

            int yIndex = Chunk.CalculateChunkIndex(pos.Y);
            int yIndexPlus6 = Chunk.CalculateChunkIndex(pos.Y + 6);
            int yIndexMinus6 = Chunk.CalculateChunkIndex(pos.Y - 6);
            Span<int> yValues = [yIndex, yIndexPlus6, yIndexMinus6];

            int zIndex = Chunk.CalculateChunkIndex(pos.Z);
            int zIndexPlus6 = Chunk.CalculateChunkIndex(pos.Z + 6);
            int zIndexMinus6 = Chunk.CalculateChunkIndex(pos.Z - 6);
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

        public IEnumerable<Chunk> GetActiveChunks()
        {
            foreach (Vector3I index in activeChunkIndexes) { yield return chunks[index]; }
        }

        public ChunkNeighbors GetChunkNeighbors(Vector3I index) => neighborsMap[index];

        void CalculateChunkNeighbors(Chunk chunk)
        {
            ChunkNeighbors neighbors = new()
            {
                Chunk = chunk
            };

            Vector3I xNeg = chunk.Index + new Vector3I(-1, 0, 0);
            Vector3I xPos = chunk.Index + new Vector3I(1, 0, 0);
            Vector3I yPos = chunk.Index + new Vector3I(0, 1, 0);
            Vector3I yNeg = chunk.Index + new Vector3I(0, -1, 0);
            Vector3I zNeg = chunk.Index + new Vector3I(0, 0, -1);
            Vector3I zPos = chunk.Index + new Vector3I(0, 0, 1);


            neighbors.XNeg = neighborsMap.TryGetValue(xNeg, out ChunkNeighbors value) ? value.Chunk : null;
            if (neighbors.XNeg != null)
            {
                neighborsMap[xNeg].XPos = chunk;
            }

            neighbors.XPos = neighborsMap.TryGetValue(xPos, out value) ? value.Chunk : null;
            if (neighbors.XPos != null)
            {
                neighborsMap[xPos].XNeg = chunk;
            }

            neighbors.YNeg = neighborsMap.TryGetValue(yNeg, out value) ? value.Chunk : null;
            if (neighbors.YNeg != null)
            {
                neighborsMap[yNeg].YPos = chunk;
            }

            neighbors.YPos = neighborsMap.TryGetValue(yPos, out value) ? value.Chunk : null;
            if (neighbors.YPos != null)
            {
                neighborsMap[yPos].YNeg = chunk;
            }

            neighbors.ZNeg = neighborsMap.TryGetValue(zNeg, out value) ? value.Chunk : null;
            if (neighbors.ZNeg != null)
            {
                neighborsMap[zNeg].ZPos = chunk;
            }

            neighbors.ZPos = neighborsMap.TryGetValue(zPos, out value) ? value.Chunk : null;
            if (neighbors.ZPos != null)
            {
                neighborsMap[zPos].ZNeg = chunk;
            }

            if (!neighborsMap.TryAdd(chunk.Index, neighbors))
            {
                neighborsMap[chunk.Index] = neighbors;
            }
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
            List<Vector3I> generatedChunks = [];

            foreach (Vector3I proximityIndex in proximityIndexes)
            {
                Vector3I index = center + proximityIndex;

                if (!chunks.ContainsKey(index))
                {
                    Chunk chunk = worldGenerator.GenerateChunk(index);
                    chunks.Add(index, chunk);

                    databaseHandler.ApplyDelta(chunks[index]);

                    generatedChunks.Add(index);
                }
                else if (unfinishedChunkIndexes.Remove(index))
                {
                    generatedChunks.Add(index);
                }

                activeChunkIndexes.Add(index);
                inactiveChunkIndexes.Remove(index);
            }

            worldGenerator.ClearCache();

            foreach (Vector3I index in generatedChunks)
            {
                Chunk chunk = chunks[index];
                CalculateChunkNeighbors(chunk);
            }

            List<Vector3I> readyChunks = [];

            foreach (Vector3I index in generatedChunks)
            {
                ChunkNeighbors neighbors = GetChunkNeighbors(index);
                neighbors.Chunk.IsReady = neighbors.AllExist();

                if (neighbors.Chunk.IsReady)
                {
                    neighbors.Chunk.CalculateActiveBlocks(neighbors);
                    readyChunks.Add(index);
                }
                else
                {
                    unfinishedChunkIndexes.Add(index);
                }
            }

            foreach (Vector3I index in readyChunks)
            {
                ChunkNeighbors neighbors = GetChunkNeighbors(index);
                neighbors.Chunk.InitializeLight(neighbors);
            }

            foreach (Vector3I index in readyChunks)
            {
                ChunkNeighbors neighbors = GetChunkNeighbors(index);
                renderer.Add(neighbors);
            }
        }

        void RemoveInactiveChunks()
        {
            foreach (Vector3I index in inactiveChunkIndexes)
            {
                Dereference(GetChunkNeighbors(index));
                chunks[index].Dispose();
                chunks.Remove(index);
                renderer.Remove(index);
            }
            inactiveChunkIndexes.Clear();
        }

        void Dereference(ChunkNeighbors neighbors)
        {
            if (neighbors.ZNeg != null)
            {
                neighborsMap[neighbors.ZNeg.Index].ZPos = null;
            }

            if (neighbors.ZPos != null)
            {
                neighborsMap[neighbors.ZPos.Index].ZNeg = null;
            }

            if (neighbors.XNeg != null)
            {
                neighborsMap[neighbors.XNeg.Index].XPos = null;
            }

            if (neighbors.XPos != null)
            {
                neighborsMap[neighbors.XPos.Index].XNeg = null;
            }

            neighborsMap.Remove(neighbors.Chunk.Index);
        }
    }
}

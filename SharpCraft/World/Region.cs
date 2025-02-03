using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using SharpCraft.Handlers;
using SharpCraft.Utility;
using SharpCraft.World.Light;

namespace SharpCraft.World
{
    public class ChunkNeighbors
    {
        public IChunk Chunk {  get; set; }
        public IChunk XNeg { get; set; }
        public IChunk XPos {  get; set; }
        public IChunk YNeg { get; set; }
        public IChunk YPos { get; set; }
        public IChunk ZNeg { get; set; }
        public IChunk ZPos { get; set; }

        public bool All()
        {
            return XNeg is not null && XPos is not null && ZNeg is not null && ZPos is not null
                && YNeg is not null && YPos is not null;
        }

        public IEnumerable<Vector3I> GetNullChunksIndexes()
        {
            if (XNeg is null)
                yield return Chunk.Index - new Vector3I(1, 0, 0);
            if (XPos is null)
                yield return Chunk.Index + new Vector3I(1, 0, 0);
            if (YNeg is null)
                yield return Chunk.Index - new Vector3I(0, 1, 0);
            if (YPos is null)
                yield return Chunk.Index + new Vector3I(0, 1, 0);
            if (ZNeg is null)
                yield return Chunk.Index - new Vector3I(0, 0, 1);
            if (ZPos is null)
                yield return Chunk.Index + new Vector3I(0, 0, 1);
        }
    }

    class Region
    {
        //Number of chunks from the center chunk to the edge of the chunk area
        readonly int apothem;

        readonly WorldGenerator worldGenerator;
        readonly DatabaseHandler databaseHandler;
        readonly RegionRenderer renderer;
        readonly LightSystem lightSystem;

        readonly Dictionary<Vector3I, IChunk> chunks = [];
        readonly Dictionary<Vector3I, ChunkNeighbors> neighborsMap = [];
        readonly List<Vector3I> proximityIndexes = [];
        readonly List<Vector3I> activeChunkIndexes = [];
        readonly HashSet<Vector3I> inactiveChunkIndexes = [];
        readonly HashSet<Vector3I> unfinishedChunkIndexes = [];

        public Region(int apothem, WorldGenerator worldGenerator, DatabaseHandler databaseHandler, RegionRenderer renderer, LightSystem lightSystem)
        {
            this.apothem = apothem;
            this.worldGenerator = worldGenerator;
            this.databaseHandler = databaseHandler;
            this.renderer = renderer;

            proximityIndexes = GenerateProximityIndexes();
            this.lightSystem = lightSystem;
        }

        public IChunk GetChunk(Vector3I index)
        {
            return chunks[index];
        }

        public void Update(Vector3 pos)
        {
            int x = FullChunk.CalculateChunkIndex(pos.X);
            int y = FullChunk.CalculateChunkIndex(pos.Y);
            int z = FullChunk.CalculateChunkIndex(pos.Z);

            Vector3I center = new(x, y, z);

            inactiveChunkIndexes.UnionWith(activeChunkIndexes);

            GenerateChunks(center);
            RemoveInactiveChunks();

            foreach (Vector3I index in activeChunkIndexes)
            {
                IChunk chunk = GetChunk(index);
                if (chunk.RecalculateMesh)
                {
                    var neighbors = GetChunkNeighbors(index);

                    if (neighbors.All())
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
            int xIndex = FullChunk.CalculateChunkIndex(pos.X);
            int xIndexPlus6 = FullChunk.CalculateChunkIndex(pos.X + 6);
            int xIndexMinus6 = FullChunk.CalculateChunkIndex(pos.X - 6);
            Span<int> xValues = [xIndex, xIndexPlus6, xIndexMinus6];

            int yIndex = FullChunk.CalculateChunkIndex(pos.Y);
            int yIndexPlus6 = FullChunk.CalculateChunkIndex(pos.Y + 6);
            int yIndexMinus6 = FullChunk.CalculateChunkIndex(pos.Y - 6);
            Span<int> yValues = [yIndex, yIndexPlus6, yIndexMinus6];

            int zIndex = FullChunk.CalculateChunkIndex(pos.Z);
            int zIndexPlus6 = FullChunk.CalculateChunkIndex(pos.Z + 6);
            int zIndexMinus6 = FullChunk.CalculateChunkIndex(pos.Z - 6);
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

        public ChunkNeighbors GetChunkNeighbors(Vector3I index)
        {
            neighborsMap.TryGetValue(index, out var res);
            return res;
        }

        void CalculateChunkNeighbors(IChunk chunk)
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
                    IChunk chunk = worldGenerator.GenerateChunk(index);
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

            foreach (Vector3I index in generatedChunks)
            {
                IChunk chunk = chunks[index];
                CalculateChunkNeighbors(chunk);
            }

            List<ChunkNeighbors> readyChunks = [];

            foreach (Vector3I index in generatedChunks)
            {
                ChunkNeighbors neighbors = GetChunkNeighbors(index);
                neighbors.Chunk.IsReady = neighbors.All();

                if (neighbors.Chunk.IsReady)
                {
                    neighbors.Chunk.CalculateActiveBlocks(neighbors);
                    if (neighbors.Chunk is not SkyChunk) readyChunks.Add(neighbors);
                }
                else
                {
                    unfinishedChunkIndexes.Add(index);
                }
            }

            List<ChunkNeighbors> skyChunks = [.. worldGenerator.GetSkyLevel().Select(GetChunkNeighbors).Where(x => x is not null && x.All())];

            foreach (ChunkNeighbors n in skyChunks)
            {
                lightSystem.InitializeSkylight(n);
            }

            foreach (ChunkNeighbors n in readyChunks)
            {
                lightSystem.InitializeLight(n);
            }

            lightSystem.FloodFill(neighborsMap);

            foreach (ChunkNeighbors n in readyChunks)
            {
                renderer.AddMesh(n);
            }

            worldGenerator.ClearCache();
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

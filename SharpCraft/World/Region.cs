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
        public Chunk ZNeg { get; set; }
        public Chunk ZPos { get; set; }
        public Chunk XNeg { get; set; }
        public Chunk XPos {  get; set; }
    }

    public class Region
    {
        //Number of chunks from the center chunk to the edge of the chunk area
        readonly int apothem;

        readonly WorldGenerator worldGenerator;
        readonly DatabaseHandler databaseHandler;
        readonly BlockMetadataProvider blockMetadata;

        readonly Dictionary<Vector3I, Chunk> chunks = [];
        readonly Dictionary<Vector3I, ChunkNeighbors> neighborsMap = [];
        readonly List<Vector3I> proximityIndexes = [];
        readonly List<Vector3I> activeChunkIndexes = [];
        readonly HashSet<Vector3I> inactiveChunkIndexes = [];

        public Region(int apothem, WorldGenerator worldGenerator, DatabaseHandler databaseHandler, BlockMetadataProvider blockMetadata)
        {
            this.apothem = apothem;
            this.worldGenerator = worldGenerator;
            this.databaseHandler = databaseHandler;
            this.blockMetadata = blockMetadata;

            proximityIndexes = GenerateProximityIndexes();
        }

        public Chunk GetChunk(Vector3I index)
        {
            return chunks[index];
        }

        public void Update(Vector3 pos)
        {
            int x = GetChunkIndex(pos.X);
            int z = GetChunkIndex(pos.Z);

            Vector3I center = new(x, 0, z);

            inactiveChunkIndexes.UnionWith(activeChunkIndexes);

            GenerateChunks(center);
            RemoveInactiveChunks();

            foreach (Vector3I index in activeChunkIndexes)
            {
                Chunk chunk = GetChunk(index);
                if (chunk.UpdateMesh)
                {
                    var neighbors = GetChunkNeighbors(index);
                    chunk.CalculateMesh(neighbors, GetVisibleFaces);
                }
            }
        }

        public Vector3I[] GetReachableChunkIndexes(Vector3 pos)
        {
            int xIndex = GetChunkIndex(pos.X);
            int xIndexPlus6 = GetChunkIndex(pos.X + 6);
            int xIndexMinus6 = GetChunkIndex(pos.X - 6);

            int zIndex = GetChunkIndex(pos.Z);
            int zIndexPlus6 = GetChunkIndex(pos.Z + 6);
            int zIndexMinus6 = GetChunkIndex(pos.Z - 6);

            return [
                new(xIndex, 0, zIndex),
                new(xIndexPlus6, 0, zIndexPlus6),
                new(xIndexMinus6, 0, zIndexMinus6),
                new(xIndex, 0, zIndexPlus6),
                new(xIndex, 0, zIndexMinus6),
                new(xIndexPlus6, 0, zIndex),
                new(xIndexMinus6, 0, zIndex)
            ];
        }

        public IEnumerable<Vector3I> GetActiveChunkIndexes()
        {
            foreach (Vector3I index in activeChunkIndexes) { yield return index; }
        }

        public ChunkNeighbors GetChunkNeighbors(Vector3I index) => neighborsMap[index];

        void CalculateChunkNeighbors(Chunk chunk)
        {
            ChunkNeighbors neighbors = new()
            {
                Chunk = chunk
            };

            Vector3I zNeg = chunk.Index + new Vector3I(0, 0, -1),
                    zPos = chunk.Index + new Vector3I(0, 0, 1),
                    xNeg = chunk.Index + new Vector3I(-1, 0, 0),
                    xPos = chunk.Index + new Vector3I(1, 0, 0);

            neighbors.ZNeg = neighborsMap.TryGetValue(zNeg, out ChunkNeighbors value) ? value.Chunk : null;
            if (neighbors.ZNeg != null)
            {
                neighborsMap[zNeg].ZPos = chunk;
            }

            neighbors.ZPos = neighborsMap.TryGetValue(zPos, out value) ? value.Chunk : null;
            if (neighbors.ZPos != null)
            {
                neighborsMap[zPos].ZNeg = chunk;
            }

            neighbors.XNeg = neighborsMap.TryGetValue(xNeg, out value) ? value.Chunk : null;
            if (neighbors.XNeg != null)
            {
                neighborsMap[xNeg].XPos = chunk;
            }

            neighbors.XPos = neighborsMap.TryGetValue(xPos, out value) ? value.Chunk : null;
            if (neighbors.XPos != null)
            {
                neighborsMap[xPos].XNeg = chunk;
            }

            neighborsMap.Add(chunk.Index, neighbors);
        }

        void CalculateActiveBlocks(ChunkNeighbors neighbors)
        {
            Chunk chunk = neighbors.Chunk;

            for (int y = 0; y < Chunk.Height; y++)
            {
                for (int x = 0; x < Chunk.Size; x++)
                {
                    for (int z = 0; z < Chunk.Size; z++)
                    {
                        if (chunk[x, y, z].IsEmpty)
                        {
                            continue;
                        }

                        FacesState visibleFaces = GetVisibleFaces(y, x, z, neighbors, calculateOpacity: false);

                        if (visibleFaces.Any())
                        {
                            chunk.AddIndex(new(x, y, z));
                        }
                    }
                }
            }
        }

        public FacesState GetVisibleFaces(int y, int x, int z, ChunkNeighbors neighbors,
                                    bool calculateOpacity = true)
        {
            FacesState visibleFaces = new();

            Chunk chunk = neighbors.Chunk;

            Block block = chunk[x, y, z];

            Block adjacentBlock;

            bool blockOpaque = true;
            if (calculateOpacity)
            {
                blockOpaque = !(block.IsEmpty || blockMetadata.IsBlockTransparent(block.Value));
            }

            if (z == Chunk.Last)
            {
                if (neighbors.ZNeg != null)
                {
                    adjacentBlock = neighbors.ZNeg[x, y, 0];
                }
                else
                {
                    adjacentBlock = new(1);
                }
            }
            else
            {
                adjacentBlock = chunk[x, y, z + 1];
            }
            visibleFaces.ZPos = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (z == 0)
            {
                if (neighbors.ZPos != null)
                {
                    adjacentBlock = neighbors.ZPos[x, y, Chunk.Last];
                }
                else
                {
                    adjacentBlock = new(1);
                }
            }
            else
            {
                adjacentBlock = chunk[x, y, z - 1];
            }
            visibleFaces.ZNeg = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (y + 1 < Chunk.Height)
            {
                adjacentBlock = chunk[x, y + 1, z];
                visibleFaces.YPos = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);
            }

            if (y > 0)
            {
                adjacentBlock = chunk[x, y - 1, z];
                visibleFaces.YNeg = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);
            }


            if (x == Chunk.Last)
            {
                if (neighbors.XNeg != null)
                {
                    adjacentBlock = neighbors.XNeg[0, y, z];
                }
                else
                {
                    adjacentBlock = new(1);
                }
            }
            else
            {
                adjacentBlock = chunk[x + 1, y, z];
            }
            visibleFaces.XPos = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (x == 0)
            {
                if (neighbors.XPos != null)
                {
                    adjacentBlock = neighbors.XPos[Chunk.Last, y, z];
                }
                else
                {
                    adjacentBlock = new(1);
                }
            }
            else
            {
                adjacentBlock = chunk[x - 1, y, z];
            }
            visibleFaces.XNeg = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            return visibleFaces;
        }

        List<Vector3I> GenerateProximityIndexes()
        {
            List<Vector3I> indexes = [];
            for (int x = -apothem; x <= apothem; x++)
            {
                for (int z = -apothem; z <= apothem; z++)
                {
                    Vector3I index = new(x, 0, z);
                    indexes.Add(index);
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

                activeChunkIndexes.Add(index);
                inactiveChunkIndexes.Remove(index);
            }

            foreach (Vector3I index in generatedChunks)
            {
                Chunk chunk = chunks[index];
                CalculateChunkNeighbors(chunk);
            }

            foreach (Vector3I index in generatedChunks)
            {
                Chunk chunk = chunks[index];
                ChunkNeighbors neighbors = GetChunkNeighbors(index);
                CalculateActiveBlocks(neighbors);
                chunk.InitializeLight(neighbors);
            }

            foreach (Vector3I index in generatedChunks)
            {
                Chunk chunk = chunks[index];
                ChunkNeighbors neighbors = GetChunkNeighbors(index);
                chunk.CalculateMesh(neighbors, GetVisibleFaces);
            }
        }

        void RemoveInactiveChunks()
        {
            foreach (Vector3I index in inactiveChunkIndexes)
            {
                Dereference(GetChunkNeighbors(index));
                chunks[index]?.Dispose();
                chunks.Remove(index);
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

        int GetChunkIndex(float val)
        {
            if (val > 0)
            {
                return -(int)Math.Floor(val / Chunk.Size);
            }

            return (int)Math.Ceiling(-val / Chunk.Size);
        }
    }
}

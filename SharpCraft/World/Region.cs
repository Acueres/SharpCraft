using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using SharpCraft.Handlers;
using SharpCraft.Utility;

namespace SharpCraft.World
{
    public class Region(int apothem, int chunkSize, WorldGenerator worldGenerator, DatabaseHandler databaseHandler)
    {
        //Number of chunks from the center chunk to the edge of the chunk area
        readonly int apothem = apothem;
        readonly int chunkSize = chunkSize;

        readonly WorldGenerator worldGenerator = worldGenerator;
        readonly DatabaseHandler databaseHandler = databaseHandler;

        readonly Dictionary<Vector3I, Chunk> chunks = [];
        readonly HashSet<Vector3I> activeChunkIndexes = [];
        readonly HashSet<Vector3I> inactiveChunkIndexes = [];

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
                chunks[index].Update();
            }
        }

        public Vector3I[] GetReachableChunkIndexes(Vector3 pos)
        {
            return [
                new(GetChunkIndex(pos.X), 0, GetChunkIndex(pos.Z)),
                new(GetChunkIndex(pos.X + 6), 0, GetChunkIndex(pos.Z + 6)),
                new(GetChunkIndex(pos.X - 6), 0, GetChunkIndex(pos.Z - 6)),
                new(GetChunkIndex(pos.X), 0, GetChunkIndex(pos.Z + 6)),
                new(GetChunkIndex(pos.X), 0, GetChunkIndex(pos.Z - 6)),
                new(GetChunkIndex(pos.X + 6), 0, GetChunkIndex(pos.Z)),
                new(GetChunkIndex(pos.X - 6), 0, GetChunkIndex(pos.Z))
            ];
        }

        public IEnumerable<Vector3I> GetActiveChunkIndexes()
        {
            foreach (Vector3I index in activeChunkIndexes) { yield return index; }
        }

        void GenerateChunks(Vector3I center)
        {
            activeChunkIndexes.Clear();
            List<Vector3I> generatedChunks = [];

            for (int x = -apothem; x <= apothem; x++)
            {
                for (int z = -apothem; z <= apothem; z++)
                {
                    Vector3I position = center - new Vector3I(x, 0, z);

                    if (!chunks.ContainsKey(position))
                    {
                        Chunk chunk = worldGenerator.GenerateChunk(position, chunks);
                        chunks.Add(position, chunk);

                        databaseHandler.ApplyDelta(chunks[position]);

                        generatedChunks.Add(position);
                    }

                    activeChunkIndexes.Add(position);
                    inactiveChunkIndexes.Remove(position);
                }
            }

            foreach (Vector3I position in generatedChunks)
            {
                Chunk chunk = chunks[position];
                chunk.GetNeighbors();
                chunk.CalculateVisibleBlock();
                chunk.InitializeLight();
                chunk.CalculateMesh();
            }
        }

        void RemoveInactiveChunks()
        {
            foreach (Vector3I position in inactiveChunkIndexes)
            {
                chunks[position]?.Dispose();
                chunks.Remove(position);
            }
            inactiveChunkIndexes.Clear();
        }

        int GetChunkIndex(float val)
        {
            if (val > 0)
            {
                return -(int)Math.Floor(val / chunkSize);
            }

            return (int)Math.Ceiling(-val / chunkSize);
        }
    }
}

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
        readonly ChunkMesher chunkMesher;

        readonly ConcurrentDictionary<Vec3<int>, Chunk> chunks = [];
        readonly List<Vec3<sbyte>> proximityIndexes = [];
        readonly ConcurrentBag<Vec3<int>> activeChunkIndexes = [];
        readonly HashSet<Vec3<int>> inactiveChunkIndexes = [];
        readonly HashSet<Vec3<int>> unfinishedChunkIndexes = [];
        readonly object chunkLock = new();

        public Region(int apothem, WorldGenerator worldGenerator, LightSystem lightSystem, ChunkMesher chunkMesher)
        {
            this.apothem = apothem;
            this.worldGenerator = worldGenerator;

            proximityIndexes = GenerateProximityIndexes();
            this.lightSystem = lightSystem;
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

                if (chunk.RecalculateMesh && chunk.AllAdjacent)
                {
                    chunk.IsReady = true;
                    chunkMesher.AddMesh(chunk);
                }
            }
        }

        public IEnumerable<Chunk> GetActiveChunks()
        {
            foreach (Vec3<int> index in activeChunkIndexes) { yield return chunks[index]; }
        }

        List<Vec3<sbyte>> GenerateProximityIndexes()
        {
            List<Vec3<sbyte>> indexes = [];
            for (int x = -apothem; x <= apothem; x++)
            {
                for (int y = -apothem; y <= apothem; y++)
                {
                    for (int z = -apothem; z <= apothem; z++)
                    {
                        Vec3<sbyte> index = new((sbyte)x, (sbyte)y, (sbyte)z);
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

            Parallel.ForEach(proximityIndexes, proximityIndex =>
            {
                Vec3<int> index = center + proximityIndex.Into<int>();

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
                    Chunk chunk = worldGenerator.GenerateChunk(index);
                    chunks.TryAdd(index, chunk);

                    generatedChunks.Add(index);

                    lock (chunkLock)
                    {
                        // XNeg neighbor
                        if (chunks.TryGetValue(index + new Vec3<int>(-1, 0, 0), out var xNegChunk))
                        {
                            chunk.XNeg = xNegChunk;
                            xNegChunk.XPos = chunk;
                        }
                        // XPos neighbor
                        if (chunks.TryGetValue(index + new Vec3<int>(1, 0, 0), out var xPosChunk))
                        {
                            chunk.XPos = xPosChunk;
                            xPosChunk.XNeg = chunk;
                        }
                        // YNeg
                        if (chunks.TryGetValue(index + new Vec3<int>(0, -1, 0), out var yNegChunk))
                        {
                            chunk.YNeg = yNegChunk;
                            yNegChunk.YPos = chunk;
                        }
                        // YPos
                        if (chunks.TryGetValue(index + new Vec3<int>(0, 1, 0), out var yPosChunk))
                        {
                            chunk.YPos = yPosChunk;
                            yPosChunk.YNeg = chunk;
                        }
                        // ZNeg
                        if (chunks.TryGetValue(index + new Vec3<int>(0, 0, -1), out var zNegChunk))
                        {
                            chunk.ZNeg = zNegChunk;
                            zNegChunk.ZPos = chunk;
                        }
                        // ZPos
                        if (chunks.TryGetValue(index + new Vec3<int>(0, 0, 1), out var zPosChunk))
                        {
                            chunk.ZPos = zPosChunk;
                            zPosChunk.ZNeg = chunk;
                        }
                    }
                }

                activeChunkIndexes.Add(index);

                lock (inactiveChunkIndexes)
                    inactiveChunkIndexes.Remove(index);
            });

            List<Chunk> readyChunks = [];

            foreach (Vec3<int> index in generatedChunks)
            {
                Chunk chunk = chunks[index];

                if (chunk.IsEmpty) continue;

                chunk.IsReady = chunk.AllAdjacent;

                if (chunk.IsReady)
                {
                    chunk.InitLight();
                    readyChunks.Add(chunk);
                }
                else
                {
                    unfinishedChunkIndexes.Add(index);
                }
            }

            List<Chunk> skyChunks = [.. worldGenerator.GetSkyLevel().Select(index => chunks[index]).Where(c => c is not null && c.IsReady && c.AllAdjacent)];

            foreach (Chunk chunk in skyChunks)
            {
                lightSystem.InitializeSkylight(chunk);
            }

            foreach (Chunk chunk in readyChunks)
            {
                lightSystem.InitializeLight(chunk);
            }

            lightSystem.Execute();

            foreach (Chunk chunk in readyChunks)
            {
                chunkMesher.AddMesh(chunk);
            }

            worldGenerator.ClearCache();
        }

        void RemoveInactiveChunks()
        {
            Parallel.ForEach(inactiveChunkIndexes, index =>
            {
                lock (chunkLock)
                {
                    chunks.Remove(index, out var chunk);
                    chunkMesher.Remove(index);

                    if (chunk.XNeg != null)
                    {
                        chunk.XNeg.XPos = null;
                        chunk.XNeg = null;
                    }
                    if (chunk.XPos != null)
                    {
                        chunk.XPos.XNeg = null;
                        chunk.XPos = null;
                    }
                    if (chunk.YNeg != null)
                    {
                        chunk.YNeg.YPos = null;
                        chunk.YNeg = null;
                    }
                    if (chunk.YPos != null)
                    {
                        chunk.YPos.YNeg = null;
                        chunk.YPos = null;
                    }
                    if (chunk.ZNeg != null)
                    {
                        chunk.ZNeg.ZPos = null;
                        chunk.ZNeg = null;
                    }
                    if (chunk.ZPos != null)
                    {
                        chunk.ZPos.ZNeg = null;
                        chunk.ZPos = null;
                    }

                    chunk.Dispose();
                }
            });
            inactiveChunkIndexes.Clear();
        }
    }
}

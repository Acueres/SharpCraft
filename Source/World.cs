using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    class World
    {
        public Dictionary<Vector3, Chunk> Region;
        public Vector3[] ActiveChunks;
        public bool UpdateOccured;

        Player player;
        GameMenu gameMenu;
        BlockHanlder blockHanlder;
        WorldGenerator worldGenerator;
        ChunkHandler chunkHandler;
        SaveHandler saveHandler;
        LightHandler lightHandler;

        HashSet<Vector3> nearChunks;
        Vector3[] loadedChunks;
        List<Vector3> inactiveChunks;

        int size;
        int renderDistance;

        ushort water;

        bool[] transparentBlocks;


        public World(int textureCount, GameMenu _gameMenu, SaveHandler _saveHandler,
            Dictionary<string, ushort> blockIndices, Dictionary<ushort, ushort[]> multifaceBlocks,
            bool[] _transparentBlocks)
        {
            gameMenu = _gameMenu;
            saveHandler = _saveHandler;
            transparentBlocks = _transparentBlocks;

            size = Parameters.ChunkSize;
            renderDistance = Parameters.RenderDistance;

            water = blockIndices["Water"];

            worldGenerator = new WorldGenerator(blockIndices);
            Region = new Dictionary<Vector3, Chunk>((int)2e3);
            chunkHandler = new ChunkHandler(worldGenerator, Region, multifaceBlocks, transparentBlocks, size, textureCount);
            lightHandler = new LightHandler(size, transparentBlocks);

            UpdateOccured = true;

            int n1 = 2 * renderDistance + 1;
            int n2 = (2 * (renderDistance + 2) + 1);
            ActiveChunks = new Vector3[n1 * n1];
            nearChunks = new HashSet<Vector3>(4);
            inactiveChunks = new List<Vector3>(n2 * n2);
            loadedChunks = new Vector3[n2 * n2];
        }

        public void SetPlayer(Player _player)
        {
            player = _player;

            blockHanlder = new BlockHanlder(player, Region, gameMenu, saveHandler, lightHandler, size);

            GetActiveChunks();

            int centerIndex = (ActiveChunks.Length - 1) / 2;

            if (Parameters.Position == Vector3.Zero)
            {
                player.Position = new Vector3(Region[ActiveChunks[centerIndex]].ActiveX[0],
                Region[ActiveChunks[centerIndex]].ActiveY[0] + 2f, Region[ActiveChunks[centerIndex]].ActiveZ[0]);
            }
            else
            {
                player.Position = Parameters.Position;
            }
        }

        public void Update()
        {
            if (UpdateOccured || player.UpdateOccured)
            {
                GetActiveChunks();
                UnloadChunks();
                PlayerInteraction();
            }
        }

        float GetChunkIndex(float val)
        {
            if (val > 0)
            {
                return -size * (float)Math.Floor(val / size);
            }
            else
            {
                return size * (float)Math.Ceiling(-val / size);
            }
        }

        void GetActiveChunks()
        {
            float x = GetChunkIndex(player.Position.X);
            float z = GetChunkIndex(player.Position.Z);

            Vector3 center = new Vector3(x, 0, z);

            inactiveChunks = loadedChunks.ToList();

            LoadRegion(center);

            GenerateRegion(center);

            for (int i = 0; i < ActiveChunks.Length; i++)
            {
                if (Region[ActiveChunks[i]].Initialize)
                {
                    chunkHandler.Initialize(Region[ActiveChunks[i]]);
                    lightHandler.Initialize(Region[ActiveChunks[i]]);
                }
            }

            for (int i = 0; i < ActiveChunks.Length; i++)
            {
                if (Region[ActiveChunks[i]].GenerateMesh)
                {
                    chunkHandler.GenerateMesh(Region[ActiveChunks[i]]);
                }
            }
        }

        void LoadRegion(Vector3 center)
        {
            int count = 0;
            int n = size * (renderDistance + 2);

            for (int i = -n; i <= n; i += size)
            {
                for (int j = -n; j <= n; j += size)
                {
                    Vector3 position = center - new Vector3(i, 0, j);
                    if (!Region.ContainsKey(position))
                    {
                        Region.Add(position, null);
                    }

                    loadedChunks[count] = position;
                    count++;
                }
            }
        }

        void GenerateRegion(Vector3 center)
        {
            int count = 0;
            int n = size * renderDistance;

            for (int i = -n; i <= n; i += size)
            {
                for (int j = -n; j <= n; j += size)
                {
                    Vector3 position = center - new Vector3(i, 0, j);
                    if (Region[position] is null)
                    {
                        Region[position] = worldGenerator.GenerateChunk(position);
                        saveHandler.ApplyDelta(Region[position]);
                    }

                    ActiveChunks[count] = position;
                    count++;
                }
            }
        }

        void UnloadChunks()
        {
            for (int i = 0; i < inactiveChunks.Count; i++)
            {
                if (loadedChunks.Contains(inactiveChunks[i]))
                {
                    inactiveChunks.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 0; i < inactiveChunks.Count; i++)
            {
                if (Region[inactiveChunks[i]] != null)
                {
                    chunkHandler.Dereference(Region[inactiveChunks[i]]);
                    Region[inactiveChunks[i]].Dispose();
                }

                Region[inactiveChunks[i]] = null;
            }

            inactiveChunks.Clear();
        }

        void PlayerInteraction()
        {
            float minDistance = 4.5f;

            blockHanlder.Reset();

            nearChunks.Add(new Vector3(GetChunkIndex(player.Position.X), 0, GetChunkIndex(player.Position.Z)));
            nearChunks.Add(new Vector3(GetChunkIndex(player.Position.X + 6), 0, GetChunkIndex(player.Position.Z + 6)));
            nearChunks.Add(new Vector3(GetChunkIndex(player.Position.X - 6), 0, GetChunkIndex(player.Position.Z - 6)));
            nearChunks.Add(new Vector3(GetChunkIndex(player.Position.X), 0, GetChunkIndex(player.Position.Z + 6)));
            nearChunks.Add(new Vector3(GetChunkIndex(player.Position.X), 0, GetChunkIndex(player.Position.Z - 6)));
            nearChunks.Add(new Vector3(GetChunkIndex(player.Position.X + 6), 0, GetChunkIndex(player.Position.Z)));
            nearChunks.Add(new Vector3(GetChunkIndex(player.Position.X - 6), 0, GetChunkIndex(player.Position.Z)));

            Vector3 blockMax = new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 blockMin = new Vector3(-0.5f, -0.5f, -0.5f);

            bool[] visibleFaces = new bool[6];

            foreach (Vector3 position in nearChunks)
            {
                for (int j = 0, n = Region[position].ActiveY.Count; j < n; j++)
                {
                    byte y = Region[position].ActiveY[j];
                    byte x = Region[position].ActiveX[j];
                    byte z = Region[position].ActiveZ[j];


                    Vector3 blockPosition = new Vector3(x, y, z) - position;

                    if ((player.Position - blockPosition).Length() > 6)
                    {
                        continue;
                    }

                    BoundingBox blockBounds = new BoundingBox(blockMin + blockPosition, blockMax + blockPosition);

                    if (player.Bound.Intersects(blockBounds))
                    {
                        if (Region[position].Blocks[y][x][z] == water)
                        {
                            if (!player.Swimming)
                            {
                                player.Physics.Velocity.Y /= 2;
                            }

                            player.Swimming = true;
                        }
                        else
                        {
                            chunkHandler.GetVisibleFaces(visibleFaces, Region[position], y, x, z);
                            player.Physics.Collision(blockPosition, visibleFaces);
                        }
                    }

                    if (player.Camera.Frustum.Contains(blockBounds) != ContainmentType.Disjoint)
                    {
                        float? rayBlockDistance = player.Ray.Intersects(blockBounds);
                        if (rayBlockDistance != null && rayBlockDistance < minDistance)
                        {
                            minDistance = (float)rayBlockDistance;
                            blockHanlder.Set(x, y, z, j, position);
                        }
                    }
                }
            }

            UpdateOccured = blockHanlder.Update();

            nearChunks.Clear();
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    class World
    {
        public Dictionary<Vector3, Chunk> Region;
        public Vector3[] ActiveChunks;
        public VertexPositionTextureLight[] Outline;

        Player player;
        GameMenu gameMenu;
        BlockHanlder blockHanlder;
        WorldGenerator worldGenerator;
        ChunkHandler chunkHandler;
        DatabaseHandler databaseHandler;
        LightHandler lightHandler;
        BlockSelector blockSelector;

        HashSet<Vector3> nearChunks;
        Vector3[] loadedChunks;
        List<Vector3> inactiveChunks;

        int size;
        int renderDistance;

        ushort water;


        public World(GameMenu _gameMenu, DatabaseHandler _databaseHandler,
            BlockSelector _blockSelector, Parameters parameters)
        {
            gameMenu = _gameMenu;
            databaseHandler = _databaseHandler;

            size = Settings.ChunkSize;
            renderDistance = Settings.RenderDistance;

            water = Assets.BlockIndices["Water"];

            worldGenerator = new WorldGenerator(parameters);
            Region = new Dictionary<Vector3, Chunk>((int)2e3);
            chunkHandler = new ChunkHandler(worldGenerator, Region, parameters);
            lightHandler = new LightHandler(size);
            blockSelector = _blockSelector;

            Outline = new VertexPositionTextureLight[36];

            int n1 = 2 * renderDistance + 1;
            int n2 = (2 * (renderDistance + 2) + 1);
            ActiveChunks = new Vector3[n1 * n1];
            nearChunks = new HashSet<Vector3>(4);
            inactiveChunks = new List<Vector3>(n2 * n2);
            loadedChunks = new Vector3[n2 * n2];
        }

        public void SetPlayer(MainGame game, Player _player, Parameters parameters)
        {
            player = _player;

            blockHanlder = new BlockHanlder(game, player, Region, gameMenu, databaseHandler, lightHandler, size);

            GetActiveChunks();

            int centerIndex = (ActiveChunks.Length - 1) / 2;

            if (parameters.Position == Vector3.Zero)
            {
                player.Position = new Vector3(Region[ActiveChunks[centerIndex]].Active[0].X,
                Region[ActiveChunks[centerIndex]].Active[0].Y + 2f, Region[ActiveChunks[centerIndex]].Active[0].Z);
            }
            else
            {
                player.Position = parameters.Position;
            }
        }

        public void Update()
        {
            if (player.UpdateOccured)
            {
                GetActiveChunks();
                UnloadChunks();
                UpdateBlocks();
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
                        databaseHandler.ApplyDelta(Region[position]);
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

        void UpdateBlocks()
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

            Chunk chunk;

            foreach (Vector3 position in nearChunks)
            {
                chunk = Region[position];
                for (int j = 0, n = chunk.Active.Count; j < n; j++)
                {
                    int y = chunk.Active[j].Y;
                    int x = chunk.Active[j].X;
                    int z = chunk.Active[j].Z;


                    Vector3 blockPosition = new Vector3(x, y, z) - position;

                    if ((player.Position - blockPosition).Length() > 6)
                    {
                        continue;
                    }

                    BoundingBox blockBounds = new BoundingBox(blockMin + blockPosition, blockMax + blockPosition);

                    if (player.Bound.Intersects(blockBounds))
                    {
                        if (chunk.Blocks[y][x][z] == water)
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

            blockHanlder.Update(blockSelector, chunkHandler);

            nearChunks.Clear();
        }
    }
}

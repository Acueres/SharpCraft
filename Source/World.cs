using Microsoft.Xna.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;


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

            size = Parameters.ChunkSize;
            renderDistance = Parameters.RenderDistance;

            water = blockIndices["Water"];

            transparentBlocks = _transparentBlocks;

            worldGenerator = new WorldGenerator(blockIndices, type: Parameters.WorldType, seed: Parameters.Seed, size: size);
            Region = new Dictionary<Vector3, Chunk>();

            chunkHandler = new ChunkHandler(worldGenerator, Region, multifaceBlocks, transparentBlocks, size, textureCount);

            UpdateOccured = true;

            ActiveChunks = new Vector3[(2 * renderDistance + 1) * (2 * renderDistance + 1)];

            inactiveChunks = new List<Vector3>((2 * (renderDistance + 2) + 1) * (2 * (renderDistance + 2) + 1));

            loadedChunks = new Vector3[(2 * (renderDistance + 2) + 1) * (2 * (renderDistance + 2) + 1)];
        }

        public void SetPlayer(Player _player, Dictionary<string, ushort> blockIndices)
        {
            player = _player;

            blockHanlder = new BlockHanlder(player, Region, gameMenu, saveHandler, size);

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
                UpdatePlayerActions();
            }
        }

        float GetChunkIndex(float val)
        {
            if (val < 0)
            {
                return size * (float)Math.Ceiling(-val / size);
            }
            else
            {
                return -size * (float)Math.Floor(val / size);
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
                }
            }

            for (int i = 0; i < ActiveChunks.Length; i++)
            {
                if (Region[ActiveChunks[i]].GenerateMesh)
                {
                    chunkHandler.GenerateMesh(Region[ActiveChunks[i]]);
                }
            }

            ActiveChunks.OrderByDescending(v => (v - player.Position).Length());
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
                    Region[inactiveChunks[i]].Dispose();

                Region[inactiveChunks[i]] = null;
            }

            inactiveChunks.Clear();
        }

        void UpdatePlayerActions()
        {
            blockHanlder.Reset();
            float minDistance = 4.5f;

            float x0, z0, xPos, zPos, xNeg, zNeg;
            x0 = GetChunkIndex(player.Position.X);
            z0 = GetChunkIndex(player.Position.Z);
            xPos = GetChunkIndex(player.Position.X + 6);
            zPos = GetChunkIndex(player.Position.Z + 6);
            xNeg = GetChunkIndex(player.Position.X - 6);
            zNeg = GetChunkIndex(player.Position.Z - 6);

            Vector3 blockMax = new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 blockMin = new Vector3(-0.5f, -0.5f, -0.5f);

            for (int i = 0, n1 = ActiveChunks.Length; i < n1; i++)
            {
                Vector3 position = ActiveChunks[i];
                
                bool chunkNearPlayer = (position.X == x0 && position.Z == z0) ||
                    (position.X == xPos && position.Z == zPos) ||
                    (position.X == xNeg && position.Z == zNeg) ||
                    (position.X == x0 && position.Z == zPos) ||
                    (position.X == x0 && position.Z == zNeg) ||
                    (position.X == xPos && position.Z == z0) ||
                    (position.X == xNeg && position.Z == z0);

                if (!chunkNearPlayer)
                {
                    continue;
                }

                for (int j = 0, n2 = Region[position].ActiveY.Count; j < n2; j++)
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

                    if (player.Bounds.Intersects(blockBounds))
                    {
                        if (Region[position].Blocks[y][x][z] == water)
                        {
                            player.Swimming = true;
                        }
                        else
                        {
                            bool[] sideVisible = new bool[6];
                            chunkHandler.GetVisibleSides(sideVisible, Region[position], y, x, z);
                            player.Physics.Collision(blockPosition, sideVisible);
                        }
                    }

                    if (player.Camera.Frustum.Contains(blockBounds) != ContainmentType.Disjoint)
                    {
                        float? rayBlockDistance = player.Ray.Intersects(blockBounds);
                        if (!(rayBlockDistance is null) && rayBlockDistance < minDistance)
                        {
                            minDistance = (float)rayBlockDistance;
                            blockHanlder.Set(x, y, z, j, position);
                        }
                    }
                }
            }

            UpdateOccured = blockHanlder.Update();
        }
    }
}

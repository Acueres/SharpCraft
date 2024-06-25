using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using SharpCraft.Handlers;
using SharpCraft.Menu;
using SharpCraft.Rendering;
using SharpCraft.Utility;


namespace SharpCraft.World
{
    class WorldManager
    {
        public Dictionary<Vector3, Chunk> Region { get => region; }

        public Vector3[] ActiveChunks;
        public VertexPositionTextureLight[] Outline;

        Player player;
        GameMenu gameMenu;
        BlockHanlder blockHanlder;
        WorldGenerator worldGenerator;
        DatabaseHandler databaseHandler;
        BlockSelector blockSelector;

        Dictionary<Vector3, Chunk> region;
        HashSet<Vector3> nearChunks;
        Vector3[] loadedChunks;
        List<Vector3> inactiveChunks;

        int size;
        int renderDistance;

        ushort water;
        bool busy = false;


        public WorldManager(GameMenu gameMenu, DatabaseHandler databaseHandler,
            BlockSelector blockSelector, Parameters parameters)
        {
            this.gameMenu = gameMenu;
            this.databaseHandler = databaseHandler;

            size = Settings.ChunkSize;
            renderDistance = Settings.RenderDistance;

            water = Assets.BlockIndices["Water"];

            worldGenerator = new WorldGenerator(parameters);
            region = new Dictionary<Vector3, Chunk>((int)2e3);
            this.blockSelector = blockSelector;

            Outline = new VertexPositionTextureLight[36];

            int n1 = 2 * renderDistance + 1; //area around the player
            int n2 = (2 * (renderDistance + 2) + 1); //buffer of 2 chunks
            ActiveChunks = new Vector3[n1 * n1];
            nearChunks = new HashSet<Vector3>(4);
            inactiveChunks = new List<Vector3>(n2 * n2);
            loadedChunks = new Vector3[n2 * n2];
        }

        public void SetPlayer(MainGame game, Player player, Parameters parameters)
        {
            this.player = player;

            blockHanlder = new BlockHanlder(game, player, region, gameMenu, databaseHandler, size);

            GetActiveChunks();
            UpdateChunks();

            int centerIndex = (ActiveChunks.Length - 1) / 2;

            if (parameters.Position == Vector3.Zero)
            {
                player.Position = new Vector3(region[ActiveChunks[centerIndex]].Active[0].X,
                region[ActiveChunks[centerIndex]].Active[0].Y + 2f, region[ActiveChunks[centerIndex]].Active[0].Z);
            }
            else
            {
                player.Position = parameters.Position;
            }
        }

        public async Task UpdateAsync()
        {
            if (!player.UpdateOccured || busy) return;

            await Task.Run(() =>
            {
                busy = true;
                GetActiveChunks();
                UpdateChunks();
                RemoveInactiveChunks();
                busy = false;
            });
        }

        public void Update()
        {
            if (!player.UpdateOccured) return;

            GetActiveChunks();
            UpdateChunks();
            RemoveInactiveChunks();
            UpdateBlocks();
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

        void UpdateChunks()
        {
            Chunk chunk;

            for (int i = 0; i < ActiveChunks.Length; i++)
            {
                chunk = region[ActiveChunks[i]];
                chunk.Update();
            }
        }

        void GetActiveChunks()
        {
            float x = GetChunkIndex(player.Position.X);
            float z = GetChunkIndex(player.Position.Z);

            Vector3 center = new(x, 0, z);

            inactiveChunks = [.. loadedChunks];

            LoadRegion(center);
            GenerateRegion(center);
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
                    if (!region.ContainsKey(position))
                    {
                        region.Add(position, null);
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
                    if (region[position] is null)
                    {
                        region[position] = new Chunk(position, worldGenerator, region);
                        databaseHandler.ApplyDelta(region[position]);
                    }

                    ActiveChunks[count] = position;
                    count++;
                }
            }
        }

        void RemoveInactiveChunks()
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
                region[inactiveChunks[i]]?.Dispose();

                region[inactiveChunks[i]] = null;
                region.Remove(inactiveChunks[i]);
            }

            inactiveChunks.Clear();
        }

        public void UpdateBlocks()
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

            Vector3 blockMax = new(0.5f, 0.5f, 0.5f);
            Vector3 blockMin = new(-0.5f, -0.5f, -0.5f);

            bool[] visibleFaces = new bool[6];

            Chunk chunk;

            foreach (Vector3 position in nearChunks)
            {
                chunk = region[position];
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

                    BoundingBox blockBounds = new(blockMin + blockPosition, blockMax + blockPosition);

                    if (player.Bound.Intersects(blockBounds))
                    {
                        if (chunk[x, y, z] == water)
                        {
                            if (!player.Swimming)
                            {
                                player.Physics.Velocity.Y /= 2;
                            }

                            player.Swimming = true;
                        }
                        else
                        {
                            visibleFaces = chunk.GetVisibleFaces(visibleFaces, y, x, z);
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

            blockHanlder.Update(blockSelector);

            nearChunks.Clear();
        }
    }
}

using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using SharpCraft.MathUtil;
using SharpCraft.Handlers;
using SharpCraft.Menu;
using SharpCraft.Rendering;
using SharpCraft.Utility;

namespace SharpCraft.World
{
    class WorldSystem
    {
        public Dictionary<Vector3I, Chunk> Region => region;

        public HashSet<Vector3I> ActiveChunkIndexes { get; }
        public VertexPositionTextureLight[] Outline;

        Player player;
        GameMenu gameMenu;
        BlockHanlder blockHanlder;
        WorldGenerator worldGenerator;
        DatabaseHandler databaseHandler;
        BlockSelector blockSelector;
        readonly BlockMetadataProvider blockMetadata;

        Dictionary<Vector3I, Chunk> region;
        HashSet<Vector3I> closeChunksIndexes;
        HashSet<Vector3I> loadedChunkIndexes;
        HashSet<Vector3I> inactiveChunkIndexes;

        int size;
        int renderDistance;

        ushort water;


        public WorldSystem(GameMenu gameMenu, DatabaseHandler databaseHandler,
            BlockSelector blockSelector, Parameters parameters, BlockMetadataProvider blockMetadata)
        {
            this.gameMenu = gameMenu;
            this.databaseHandler = databaseHandler;
            this.blockMetadata = blockMetadata;

            size = Settings.ChunkSize;
            renderDistance = Settings.RenderDistance;

            water = blockMetadata.GetBlockIndex("water");

            worldGenerator = new WorldGenerator(parameters, blockMetadata);
            region = new Dictionary<Vector3I, Chunk>((int)2e3);
            this.blockSelector = blockSelector;

            Outline = new VertexPositionTextureLight[36];

            int n1 = 2 * renderDistance + 1; //area around the player
            int n2 = (2 * (renderDistance + 2) + 1); //buffer of 2 chunks
            ActiveChunkIndexes = new(n1 * n1);
            closeChunksIndexes = new HashSet<Vector3I>(4);
            inactiveChunkIndexes = new(n2 * n2);
            loadedChunkIndexes = new(n2 * n2);
        }

        public void SetPlayer(MainGame game, Player player, Parameters parameters)
        {
            this.player = player;

            blockHanlder = new BlockHanlder(game, player, region, gameMenu, databaseHandler, blockMetadata, size);

            GetActiveChunks();
            UpdateChunks();

            /*int n1 = 2 * renderDistance + 1; //area around the player
            int n2 = (2 * (renderDistance + 2) + 1); //buffer of 2 chunks
            int centerIndex = (n1 * n2 - 1) / 2;*/

            if (parameters.Position == Vector3.Zero)
            {
                Chunk center = region[new(0, 0, 0)];
                Vector3I spawningIndex = center.GetIndex(0);
                player.Position = new Vector3(spawningIndex.X, spawningIndex.Y + 2f, spawningIndex.Z);
            }
            else
            {
                player.Position = parameters.Position;
            }
        }

        public void Update()
        {
            GetActiveChunks();
            UpdateChunks();
            RemoveInactiveChunks();
        }

        int GetChunkIndex(float val)
        {
            if (val > 0)
            {
                return -(int)Math.Floor(val / size);
            }
            else
            {
                return (int)Math.Ceiling(-val / size);
            }
        }

        void UpdateChunks()
        {
            foreach (Vector3I index in ActiveChunkIndexes)
            {
                Chunk chunk = region[index];
                chunk.Update();
            }
        }

        void GetActiveChunks()
        {
            int x = GetChunkIndex(player.Position.X);
            int z = GetChunkIndex(player.Position.Z);

            Vector3I center = new(x, 0, z);

            inactiveChunkIndexes = [.. loadedChunkIndexes];

            LoadRegion(center);
            GenerateRegion(center);
        }

        void LoadRegion(Vector3I center)
        {
            int count = 0;
            int n = renderDistance + 2;
            loadedChunkIndexes.Clear();

            for (int i = -n; i <= n; i ++)
            {
                for (int j = -n; j <= n; j ++)
                {
                    Vector3I position = center - new Vector3I(i, 0, j);
                    if (!region.ContainsKey(position))
                    {
                        region.Add(position, null);
                    }

                    loadedChunkIndexes.Add(position);
                    count++;
                }
            }
        }

        void GenerateRegion(Vector3I center)
        {
            int n = renderDistance;

            List<Vector3I> generatedChunks = [];

            for (int i = -n; i <= n; i++)
            {
                for (int j = -n; j <= n; j++)
                {
                    Vector3I position = center - new Vector3I(i, 0, j);
                    if (region[position] is null)
                    {
                        Chunk chunk = worldGenerator.GenerateChunk(position, region);
                        region[position] = chunk;

                        databaseHandler.ApplyDelta(region[position]);

                        generatedChunks.Add(position);
                    }

                    ActiveChunkIndexes.Add(position);
                }
            }

            foreach (Vector3I position in generatedChunks)
            {
                Chunk chunk = region[position];
                chunk.GetNeighbors();
                chunk.CalculateVisibleBlock();
                chunk.InitializeLight();
                chunk.CalculateMesh();
            }
        }

        void RemoveInactiveChunks()
        {
            inactiveChunkIndexes.ExceptWith(loadedChunkIndexes);
            ActiveChunkIndexes.ExceptWith(inactiveChunkIndexes);
            foreach (Vector3I position in inactiveChunkIndexes)
            {
                region[position]?.Dispose();
                region.Remove(position);
            }
            inactiveChunkIndexes.Clear();
        }

        public void UpdateBlocks()
        {
            float minDistance = 4.5f;

            blockHanlder.Reset();

            closeChunksIndexes.Add(new Vector3I(GetChunkIndex(player.Position.X), 0, GetChunkIndex(player.Position.Z)));
            closeChunksIndexes.Add(new Vector3I(GetChunkIndex(player.Position.X + 6), 0, GetChunkIndex(player.Position.Z + 6)));
            closeChunksIndexes.Add(new Vector3I(GetChunkIndex(player.Position.X - 6), 0, GetChunkIndex(player.Position.Z - 6)));
            closeChunksIndexes.Add(new Vector3I(GetChunkIndex(player.Position.X), 0, GetChunkIndex(player.Position.Z + 6)));
            closeChunksIndexes.Add(new Vector3I(GetChunkIndex(player.Position.X), 0, GetChunkIndex(player.Position.Z - 6)));
            closeChunksIndexes.Add(new Vector3I(GetChunkIndex(player.Position.X + 6), 0, GetChunkIndex(player.Position.Z)));
            closeChunksIndexes.Add(new Vector3I(GetChunkIndex(player.Position.X - 6), 0, GetChunkIndex(player.Position.Z)));

            Vector3 blockMax = new(0.5f, 0.5f, 0.5f);
            Vector3 blockMin = new(-0.5f, -0.5f, -0.5f);

            bool[] visibleFaces = new bool[6];

            Chunk chunk;

            foreach (Vector3I chunkIndex in closeChunksIndexes)
            {
                chunk = region[chunkIndex];
                foreach (Vector3I blockIndex in chunk.GetIndexes())
                {
                    int y = blockIndex.Y;
                    int x = blockIndex.X;
                    int z = blockIndex.Z;

                    Vector3 blockPosition = new Vector3(x, y, z) - chunk.Position3;

                    if ((player.Position - blockPosition).Length() > 6)
                    {
                        continue;
                    }

                    BoundingBox blockBounds = new(blockPosition + blockMin, blockPosition + blockMax);

                    if (player.Bound.Intersects(blockBounds))
                    {
                        if (chunk[x, y, z].Value == water)
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
                            blockHanlder.Set(x, y, z, chunkIndex);
                        }
                    }
                }
            }

            blockHanlder.Update(blockSelector);

            closeChunksIndexes.Clear();
        }
    }
}

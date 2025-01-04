﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Assets;
using SharpCraft.Handlers;
using SharpCraft.Menu;
using SharpCraft.Utility;
using SharpCraft.World.Light;
using System.Collections.Generic;

namespace SharpCraft.World
{
    class WorldSystem
    {
        public VertexPositionTextureLight[] Outline;

        Player player;
        BlockHanlder blockHanlder;

        readonly GameMenu gameMenu;
        readonly WorldGenerator worldGenerator;
        readonly DatabaseHandler databaseHandler;
        readonly BlockSelector blockSelector;
        readonly BlockMetadataProvider blockMetadata;
        readonly Time time;
        readonly LightSystem lightSystem;

        readonly Region region;

        readonly ushort water;


        public WorldSystem(GameMenu gameMenu, DatabaseHandler databaseHandler,
            BlockSelector blockSelector, Parameters parameters, BlockMetadataProvider blockMetadata, Time time,
            AssetServer assetServer, GraphicsDevice graphicsDevice, ScreenshotHandler screenshotHandler)
        {
            this.gameMenu = gameMenu;
            this.databaseHandler = databaseHandler;
            this.blockMetadata = blockMetadata;
            this.time = time;

            water = blockMetadata.GetBlockIndex("water");

            worldGenerator = new WorldGenerator(parameters, blockMetadata);
            this.blockSelector = blockSelector;

            Outline = new VertexPositionTextureLight[36];

            lightSystem = new(blockMetadata);
            RegionRenderer regionRenderer = new(graphicsDevice, screenshotHandler, blockSelector, assetServer, blockMetadata, lightSystem);
            region = new Region(Settings.RenderDistance, worldGenerator, databaseHandler, regionRenderer, lightSystem);
        }

        public void SetPlayer(MainGame game, Player player, Parameters parameters)
        {
            this.player = player;
            player.Flying = true;

            blockHanlder = new BlockHanlder(game, player, region, gameMenu, databaseHandler, blockMetadata, lightSystem);

            region.Update(player.Position);

            if (parameters.Position == Vector3.Zero)
            {
                //Chunk center = region.GetChunk(new(0, 0, 0));
                //Vector3I spawningIndex = center.GetIndex(0);
                //player.Position = new Vector3(spawningIndex.X, spawningIndex.Y + 2f, spawningIndex.Z);
                player.Position = new Vector3(0, 100, 0);
            }
            else
            {
                player.Position = parameters.Position;
            }
        }

        public void Update()
        {
            region.Update(player.Position);
        }

        public void Render()
        {
            region.Render(player, time);
        }

        public Region GetRegion() => region;

        public void UpdateBlocks()
        {
            float minDistance = 4.5f;

            blockHanlder.Reset();

            Vector3 blockMax = new(0.5f, 0.5f, 0.5f);
            Vector3 blockMin = new(-0.5f, -0.5f, -0.5f);

            Chunk chunk;

            HashSet<Vector3I> reachableChunkIndexes = Region.GetReachableChunkIndexes(player.Position);

            foreach (Vector3I chunkIndex in reachableChunkIndexes)
            {
                chunk = region.GetChunk(chunkIndex);
                foreach (Vector3I blockIndex in chunk.GetActiveIndexes())
                {
                    int x = blockIndex.X;
                    int y = blockIndex.Y;
                    int z = blockIndex.Z;

                    Vector3 blockPosition = new Vector3(x, y, z) + chunk.Position;

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
                            var neighbors = region.GetChunkNeighbors(chunkIndex);
                            FacesState visibleFaces = chunk.GetVisibleFaces(y, x, z, neighbors);
                            player.Physics.Collision(blockPosition, visibleFaces);
                        }
                    }

                    if (player.Camera.Frustum.Contains(blockBounds) != ContainmentType.Disjoint)
                    {
                        float? rayBlockDistance = player.Ray.Intersects(blockBounds);
                        if (rayBlockDistance != null && rayBlockDistance < minDistance)
                        {
                            minDistance = (float)rayBlockDistance;
                            var neighbors = region.GetChunkNeighbors(chunkIndex);
                            blockHanlder.Set(x, y, z, chunkIndex, neighbors);
                        }
                    }
                }
            }

            blockHanlder.Update(blockSelector);
        }
    }
}

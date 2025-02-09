using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Assets;
using SharpCraft.GUI.Menus;
using SharpCraft.Persistence;
using SharpCraft.Utility;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.World.ChunkSystems;
using SharpCraft.World.Generation;
using SharpCraft.World.Light;
using System.Collections.Generic;

namespace SharpCraft.World
{
    class WorldSystem
    {
        Player player;
        readonly BlockModificationSystem blockModSystem;

        readonly GameMenu gameMenu;
        readonly WorldGenerator worldGenerator;
        readonly BlockSelector blockSelector;
        readonly Time time;
        readonly LightSystem lightSystem;
        readonly AdjacencyGraph adjacencyGraph;

        readonly Region region;

        readonly ushort water;

        public WorldSystem(GameMenu gameMenu, DatabaseService db,
            BlockSelector blockSelector, Parameters parameters, BlockMetadataProvider blockMetadata, Time time,
            AssetServer assetServer, GraphicsDevice graphicsDevice, ScreenshotTaker screenshotHandler)
        {
            this.gameMenu = gameMenu;
            this.time = time;

            water = blockMetadata.GetBlockIndex("water");

            worldGenerator = new WorldGenerator(parameters, db, blockMetadata);
            this.blockSelector = blockSelector;

            adjacencyGraph = new();
            lightSystem = new(blockMetadata, adjacencyGraph);

            blockModSystem = new BlockModificationSystem(db, blockMetadata, lightSystem);

            RegionRenderer regionRenderer = new(graphicsDevice, screenshotHandler, blockSelector, assetServer, blockMetadata, lightSystem);
            region = new Region(Settings.RenderDistance, adjacencyGraph, worldGenerator, regionRenderer, lightSystem);
        }

        public void SetPlayer(Player player, Parameters parameters)
        {
            this.player = player;
            player.Flying = true;

            region.Update(player.Position);

            if (parameters.Position == Vector3.Zero)
            {
                player.Position = new Vector3(0, 100, 0);
            }
            else
            {
                player.Position = parameters.Position;
            }
        }

        public void Update(bool updateRegion)
        {
            if (updateRegion)
            {
                region.Update(player.Position);
            }

            region.UpdateMeshes();
        }

        public void Render()
        {
            region.Render(player, time);
        }

        public Region GetRegion() => region;

        public void UpdateBlocks(bool exitedMenu)
        {
            if (exitedMenu) return;

            float minDistance = 4.5f;

            Vector3 blockMax = new(0.5f, 0.5f, 0.5f);
            Vector3 blockMin = new(-0.5f, -0.5f, -0.5f);

            IChunk chunk = null;
            ChunkAdjacency adjacency = null;
            Vector3I blockIndex = new(-1, -1, -1);

            HashSet<Vector3I> reachableChunkIndexes = Region.GetReachableChunkIndexes(player.Position);

            foreach (Vector3I chunkIndex in reachableChunkIndexes)
            {
                chunk = region.GetChunk(chunkIndex);
                foreach (Vector3I index in chunk.GetActiveIndexes())
                {
                    int x = index.X;
                    int y = index.Y;
                    int z = index.Z;

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
                            FacesState visibleFaces = chunk.GetVisibleFaces(index, adjacencyGraph.GetAdjacency(chunkIndex));
                            player.Physics.Collision(blockPosition, visibleFaces);
                        }
                    }

                    if (player.Camera.Frustum.Contains(blockBounds) != ContainmentType.Disjoint)
                    {
                        float? rayBlockDistance = player.Ray.Intersects(blockBounds);
                        if (rayBlockDistance != null && rayBlockDistance < minDistance)
                        {
                            minDistance = (float)rayBlockDistance;
                            adjacency = adjacencyGraph.GetAdjacency(chunkIndex);
                            blockIndex = index;
                        }
                    }
                }
            }

            if (player.LeftClick && blockIndex.X != -1)
            {
                blockModSystem.Add(blockIndex, adjacency, BlockInteractionMode.Remove);
            }
            else if (player.RightClick && blockIndex.X != -1)
            {
                Vector3 blockPosition = new Vector3(blockIndex.X, blockIndex.Y, blockIndex.Z) + chunk.Position;

                if ((blockPosition - player.Position).Length() > 1.1f)
                {
                    blockModSystem.Add(new Block(gameMenu.SelectedItem), blockIndex, player.Camera.Direction, adjacency, BlockInteractionMode.Add);
                }
            }

            if (adjacency is not null)
            {
                FacesState visibleFaces = chunk.GetVisibleFaces(blockIndex, adjacency);
                blockSelector.Update(visibleFaces, new Vector3(blockIndex.X, blockIndex.Y, blockIndex.Z) + adjacency.Root.Position, player.Camera.Direction);
            }
            else
            {
                blockSelector.Clear();
            }

            blockModSystem.Update();
        }
    }
}

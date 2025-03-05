using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Assets;
using SharpCraft.GUI.Menus;
using SharpCraft.MathUtilities;
using SharpCraft.Persistence;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.World.Generation;
using SharpCraft.World.Light;
using System.Collections.Generic;
using System.Xml;

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

        public void UpdateEntities(bool exitedMenu)
        {
            if (player.UpdateOccured)
            {
                UpdatePlayerAction(exitedMenu);
            }

            blockModSystem.Update();
        }

        public void Render()
        {
            region.Render(player, time);
        }

        public void UpdatePlayerAction(bool exitedMenu)
        {
            if (exitedMenu) return;

            Raycaster raycaster = new(new Vector3(player.Position.X, player.Position.Y + 1.75f, player.Position.Z),
                player.Camera.Direction, 0.1f);

            const float maxDistance = 4.5f;

            Vector3 blockPosition = player.Position;
            Vector3I chunkIndex = Chunk.WorldToChunkCoords(blockPosition);
            Vector3I blockIndex = Chunk.WorldtoBlockCoords(blockPosition);
            Block block = Block.Empty;

            while (raycaster.Length(blockPosition) < maxDistance)
            {
                blockPosition = raycaster.Step();
                chunkIndex = Chunk.WorldToChunkCoords(blockPosition);
                blockIndex = Chunk.WorldtoBlockCoords(blockPosition);

                var chunk = region.GetChunk(chunkIndex);
                block = chunk[blockIndex.X, blockIndex.Y, blockIndex.Z];
                if (!block.IsEmpty) break;
            }

            if (block.IsEmpty)
            {
                blockSelector.Clear();
                return;
            }

            ChunkAdjacency adjacency = adjacencyGraph.GetAdjacency(chunkIndex);

            if (player.LeftClick)
            {
                blockModSystem.Add(blockIndex, adjacency, BlockInteractionMode.Remove);
            }
            else if (player.RightClick)
            {
                if ((blockPosition - player.Position).Length() > 1.1f)
                {
                    blockModSystem.Add(new Block(gameMenu.SelectedItem), blockIndex, player.Camera.Direction, adjacency, BlockInteractionMode.Add);
                }
            }

            FacesState visibleFaces = adjacency.Root.GetVisibleFaces(blockIndex, adjacency);
            blockSelector.Update(visibleFaces, new Vector3(blockIndex.X, blockIndex.Y, blockIndex.Z) + adjacency.Root.Position, player.Camera.Direction);
        }

        public void UpdateBlocks(bool exitedMenu)
        {
            if (exitedMenu) return;

            HashSet<Vector3I> reachableChunkIndexes = Region.GetReachableChunkIndexes(player.Position);

            foreach (Vector3I chunkIndex in reachableChunkIndexes)
            {
                var chunk = region.GetChunk(chunkIndex);
                foreach (Vector3I index in chunk.GetActiveIndexes())
                {
                    int x = index.X;
                    int y = index.Y;
                    int z = index.Z;

                    Vector3 blockPosition = new Vector3(x, y, z) + chunk.Position;

                    BoundingBox blockBounds = new(blockPosition, blockPosition + Vector3.One);

                    player.Physics.ResolveCollision(blockBounds);
                }
            }
        }
    }
}

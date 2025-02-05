using Microsoft.Xna.Framework;
using SharpCraft.World;

using SharpCraft.Utility;
using Newtonsoft.Json.Linq;
using SharpCraft.World.Light;
using SharpCraft.World.Chunks;
using SharpCraft.GUI.Menus;
using SharpCraft.Persistence;


namespace SharpCraft.World.Blocks
{
    class BlockHanlder
    {
        MainGame game;
        Player player;
        readonly Region region;
        GameMenu gameMenu;
        DatabaseService databaseHandler;
        readonly BlockMetadataProvider blockMetadata;
        readonly LightSystem lightSystem;

        int x, y, z;
        Vector3I position;
        ChunkAdjacency adjacency;


        public BlockHanlder(MainGame game, Player player, Region region,
            GameMenu gameMenu, DatabaseService databaseHandler, BlockMetadataProvider blockMetadata, LightSystem lightSystem)
        {
            this.game = game;
            this.player = player;
            this.region = region;
            this.gameMenu = gameMenu;
            this.databaseHandler = databaseHandler;
            this.blockMetadata = blockMetadata;
            this.lightSystem = lightSystem;
        }

        public void Reset()
        {
            y = -1;
        }

        public void Set(int x, int y, int z, Vector3I position, ChunkAdjacency adjacency)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.position = position;
            this.adjacency = adjacency;
        }

        public void Update(BlockSelector blockSelector)
        {
            if (player.LeftClick && !game.ExitedMenu)
            {
                player.SetLastClickTime();
                RemoveBlock(adjacency);
            }

            else if (player.RightClick)
            {
                player.SetLastClickTime();
                AddBlock(adjacency);
            }

            if (y == -1)
            {
                blockSelector.Clear();
                return;
            }

            IChunk chunk = region.GetChunk(position);
            if (!chunk[x, y, z].IsEmpty)
            {
                FacesState visibleFaces = chunk.GetVisibleFaces(y, x, z, adjacency);

                blockSelector.Update(visibleFaces, new Vector3(x, y, z) + region.GetChunk(position).Position, player.Camera.Direction);
            }
        }

        void RemoveBlock(ChunkAdjacency adjacency)
        {
            if (y < 1)
            {
                return;
            }

            IChunk chunk = region.GetChunk(position);

            Block block = chunk[x, y, z];

            bool lightSource = !block.IsEmpty && blockMetadata.IsLightSource(block.Value);

            chunk[x, y, z] = Block.Empty;

            chunk.RemoveIndex(new(x, y, z));

            lightSystem.UpdateLight(x, y, z, Block.EmptyValue, adjacency, sourceRemoved: lightSource);

            databaseHandler.AddDelta(position, y, x, z, Block.Empty);

            UpdateAdjacentBlocks(adjacency, y, x, z);
        }

        void AddBlock(ChunkAdjacency adjacency)
        {
            if (y == -1)
            {
                return;
            }

            char side = Util.MaxVectorComponent(player.Camera.Direction);

            AdjustIndices(side, player.Camera.Direction);
            IChunk chunk = region.GetChunk(position);

            Vector3 blockPosition = new Vector3(x, y, z) + chunk.Position;

            if ((blockPosition - player.Position).Length() < 1.1f)
            {
                return;
            }
            ushort texture;

            if (chunk[x, y, z].IsEmpty && gameMenu.SelectedItem != Block.EmptyValue)
            {
                texture = gameMenu.SelectedItem;
            }
            else
            {
                return;
            }

            chunk[x, y, z] = new(texture);

            lightSystem.UpdateLight(x, y, z, texture, adjacency);

            databaseHandler.AddDelta(position, y, x, z, new(texture));

            UpdateAdjacentBlocks(adjacency, y, x, z);
        }

        static void UpdateAdjacentBlocks(ChunkAdjacency adjacency, int y, int x, int z)
        {
            IChunk chunk = adjacency.Root;
            chunk.RecalculateMesh = true;

            if (!chunk[x, y, z].IsEmpty)
            {
                ActivateBlock(chunk, y, x, z);
            }

            ActivateBlock(chunk, y + 1, x, z);
            ActivateBlock(chunk, y - 1, x, z);

            if (x < Chunk.Last)
                ActivateBlock(chunk, y, x + 1, z);
            else if (x == Chunk.Last)
                ActivateBlock(adjacency.XPos.Root, y, 0, z);

            if (x > 0)
                ActivateBlock(chunk, y, x - 1, z);
            else if (x == 0)
                ActivateBlock(adjacency.XNeg.Root, y, Chunk.Last, z);

            if (z < Chunk.Last)
                ActivateBlock(chunk, y, x, z + 1);
            else if (z == Chunk.Last)
                ActivateBlock(adjacency.ZPos.Root, y, x, 0);

            if (z > 0)
                ActivateBlock(chunk, y, x, z - 1);
            else if (z == 0)
                ActivateBlock(adjacency.ZNeg.Root, y, x, Chunk.Last);
        }

        static void ActivateBlock(IChunk chunk, int y, int x, int z)
        {
            if (!chunk[x, y, z].IsEmpty)
            {
                chunk.AddIndex(new(x, y, z));
                chunk.RecalculateMesh = true;
            }
        }

        void AdjustIndices(char face, Vector3 vector)
        {
            Vector3I zNeg = position + new Vector3I(0, 0, -1),
                    zPos = position + new Vector3I(0, 0, 1),
                    xNeg = position + new Vector3I(-1, 0, 0),
                    xPos = position + new Vector3I(1, 0, 0);


            switch (face)
            {
                case 'X':
                    if (vector.X > 0) x--;
                    else x++;

                    if (x > Chunk.Last)
                    {
                        position = xNeg;
                        x = 0;
                    }

                    else if (x < 0)
                    {
                        position = xPos;
                        x = Chunk.Last;
                    }

                    break;

                case 'Y':
                    if (vector.Y > 0) y--;
                    else y++;

                    break;

                case 'Z':
                    if (vector.Z > 0) z--;
                    else z++;

                    if (z > Chunk.Last)
                    {
                        position = zNeg;
                        z = 0;
                    }

                    else if (z < 0)
                    {
                        position = zPos;
                        z = Chunk.Last;
                    }

                    break;
            }
        }
    }
}

using Microsoft.Xna.Framework;

using SharpCraft.Menu;
using SharpCraft.Rendering;
using SharpCraft.World;

using SharpCraft.Utility;


namespace SharpCraft.Handlers
{
    class BlockHanlder
    {
        MainGame game;
        Player player;
        readonly Region region;
        GameMenu gameMenu;
        DatabaseHandler databaseHandler;
        readonly BlockMetadataProvider blockMetadata;

        int x, y, z;
        Vector3I position;
        ChunkNeighbors neighbors;


        public BlockHanlder(MainGame game, Player player, Region region,
            GameMenu gameMenu, DatabaseHandler databaseHandler, BlockMetadataProvider blockMetadata)
        {
            this.game = game;
            this.player = player;
            this.region = region;
            this.gameMenu = gameMenu;
            this.databaseHandler = databaseHandler;
            this.blockMetadata = blockMetadata;
        }

        public void Reset()
        {
            y = -1;
        }

        public void Set(int x, int y, int z, Vector3I position, ChunkNeighbors neighbors)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.position = position;
            this.neighbors = neighbors;
        }

        public void Update(BlockSelector blockSelector)
        {
            if (player.LeftClick && !game.ExitedMenu)
            {
                player.SetLastClickTime();
                RemoveBlock(neighbors);
            }

            else if (player.RightClick)
            {
                player.SetLastClickTime();
                AddBlock(neighbors);
            }

            if (y != -1 && !region.GetChunk(position)[x, y, z].IsEmpty)
            {
                bool[] visibleFaces = new bool[6];
                region.GetVisibleFaces(y, x, z, visibleFaces, neighbors);

                blockSelector.Update(visibleFaces, new Vector3(x, y, z) - region.GetChunk(position).Position, player.Camera.Direction);
            }
            else
            {
                blockSelector.Clear();
            }
        }

        void RemoveBlock(ChunkNeighbors neighbors)
        {
            if (y < 1)
            {
                return;
            }

            Chunk chunk = region.GetChunk(position);

            Block block = chunk[x, y, z];

            bool lightSource = !block.IsEmpty && blockMetadata.IsLightSource(block.Value);

            chunk[x, y, z] = Block.Empty;

            chunk.RemoveIndex(new(x, y, z));

            chunk.UpdateLight(y, x, z, Block.EmptyValue, neighbors, sourceRemoved: lightSource);

            databaseHandler.AddDelta(position, y, x, z, Block.EmptyValue);

            UpdateAdjacentBlocks(neighbors, y, x, z);
        }

        void AddBlock(ChunkNeighbors neighbors)
        {
            if (y == -1)
            {
                return;
            }

            char side = Util.MaxVectorComponent(player.Camera.Direction);

            AdjustIndices(side, player.Camera.Direction);
            Chunk chunk = region.GetChunk(position);

            Vector3 blockPosition = new Vector3(x, y, z) - chunk.Position;

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

            chunk.UpdateLight(y, x, z, texture, neighbors);

            databaseHandler.AddDelta(position, y, x, z, texture);

            UpdateAdjacentBlocks(neighbors, y, x, z);
        }

        void UpdateAdjacentBlocks(ChunkNeighbors neighbors, int y, int x, int z)
        {
            Chunk chunk = neighbors.Chunk;
            chunk.UpdateMesh = true;

            if (!chunk[x, y, z].IsEmpty)
            {
                ActivateBlock(chunk, y, x, z);
            }

            ActivateBlock(chunk, y + 1, x, z);
            ActivateBlock(chunk, y - 1, x, z);

            if (x < Chunk.LAST)
                ActivateBlock(chunk, y, x + 1, z);
            else if (x == Chunk.LAST)
                ActivateBlock(neighbors.XNeg, y, 0, z);

            if (x > 0)
                ActivateBlock(chunk, y, x - 1, z);
            else if (x == 0)
                ActivateBlock(neighbors.XPos, y, Chunk.LAST, z);

            if (z < Chunk.LAST)
                ActivateBlock(chunk, y, x, z + 1);
            else if (z == Chunk.LAST)
                ActivateBlock(neighbors.ZNeg, y, x, 0);

            if (z > 0)
                ActivateBlock(chunk, y, x, z - 1);
            else if (z == 0)
                ActivateBlock(neighbors.ZPos, y, x, Chunk.LAST);
        }

        void ActivateBlock(Chunk chunk, int y, int x, int z)
        {
            if (chunk[x, y, z].IsEmpty)
            {
                return;
            }

            chunk.AddIndex(new(x, y, z));

            chunk.UpdateMesh = true;
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

                    if (x > Chunk.LAST)
                    {
                        position = xNeg;
                        x = 0;
                    }

                    else if (x < 0)
                    {
                        position = xPos;
                        x = Chunk.LAST;
                    }

                    break;

                case 'Y':
                    if (vector.Y > 0) y--;
                    else y++;

                    break;

                case 'Z':
                    if (vector.Z > 0) z--;
                    else z++;

                    if (z > Chunk.LAST)
                    {
                        position = zNeg;
                        z = 0;
                    }

                    else if (z < 0)
                    {
                        position = zPos;
                        z = Chunk.LAST;
                    }

                    break;
            }
        }
    }
}

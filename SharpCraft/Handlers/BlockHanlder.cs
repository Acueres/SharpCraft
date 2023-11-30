using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using SharpCraft.Menu;
using SharpCraft.Rendering;
using SharpCraft.World;
using SharpCraft.Models;

using SharpCraft.Utility;


namespace SharpCraft.Handlers
{
    class BlockHanlder
    {
        MainGame game;
        Player player;
        Dictionary<Vector3, Chunk> region;
        GameMenu gameMenu;
        DatabaseHandler databaseHandler;

        IList<bool> lightSources;

        int size, last;

        int x, y, z, index;
        Vector3 position;


        public BlockHanlder(MainGame game, Player player, Dictionary<Vector3, Chunk> region,
            GameMenu gameMenu, DatabaseHandler databaseHandler, int size)
        {
            this.game = game;
            this.player = player;
            this.region = region;
            this.gameMenu = gameMenu;
            this.databaseHandler = databaseHandler;

            lightSources = Assets.LightSources;

            this.size = size;
            last = size - 1;
        }

        public void Reset()
        {
            y = -1;
        }

        public void Set(int x, int y, int z, int index, Vector3 position)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.index = index;
            this.position = position;
        }

        public void Update(BlockSelector blockSelector)
        {
            if (player.LeftClick && !game.ExitedMenu)
            {
                player.SetLastClickTime();
                RemoveBlock();
            }

            else if (player.RightClick)
            {
                player.SetLastClickTime();
                AddBlock();
            }

            if (y != -1 && region[position][x, y, z] != null)
            {
                bool[] visibleFaces = new bool[6];
                region[position].GetVisibleFaces(visibleFaces, y, x, z);

                blockSelector.Update(visibleFaces, new Vector3(x, y, z) - position, player.Camera.Direction);
            }
            else
            {
                blockSelector.Clear();
            }
        }

        void RemoveBlock()
        {
            if (y < 1)
            {
                return;
            }

            Chunk chunk = region[position];

            bool lightSource = lightSources[(ushort)chunk[x, y, z]];

            chunk[x, y, z] = null;

            chunk.Active.RemoveAt(index);

            chunk.UpdateLight(y, x, z, null, sourceRemoved: lightSource);

            databaseHandler.AddDelta(position, y, x, z, null);

            UpdateAdjacentBlocks(chunk, y, x, z);
        }

        void AddBlock()
        {
            if (y == -1)
            {
                return;
            }

            char side = Util.MaxVectorComponent(player.Camera.Direction);

            AdjustIndices(side, player.Camera.Direction);

            Vector3 blockPosition = new Vector3(x, y, z) - position;

            if ((blockPosition - player.Position).Length() < 1.1f)
            {
                return;
            }

            Chunk chunk = region[position];
            ushort? texture;

            if (chunk[x, y, z] is null && gameMenu.SelectedItem != null)
            {
                texture = gameMenu.SelectedItem;
            }
            else
            {
                return;
            }

            chunk[x, y, z] = texture;

            chunk.UpdateLight(y, x, z, texture);

            databaseHandler.AddDelta(position, y, x, z, texture);

            UpdateAdjacentBlocks(chunk, y, x, z);
        }

        void UpdateAdjacentBlocks(Chunk chunk, int y, int x, int z)
        {
            chunk.UpdateMesh = true;

            if (chunk[x, y, z] != null)
            {
                ActivateBlock(chunk, y, x, z);
            }

            ActivateBlock(chunk, y + 1, x, z);
            ActivateBlock(chunk, y - 1, x, z);

            if (x < last)
                ActivateBlock(chunk, y, x + 1, z);
            else if (x == last)
                ActivateBlock(chunk.Neighbors.XNeg, y, 0, z);

            if (x > 0)
                ActivateBlock(chunk, y, x - 1, z);
            else if (x == 0)
                ActivateBlock(chunk.Neighbors.XPos, y, last, z);

            if (z < last)
                ActivateBlock(chunk, y, x, z + 1);
            else if (z == last)
                ActivateBlock(chunk.Neighbors.ZNeg, y, x, 0);

            if (z > 0)
                ActivateBlock(chunk, y, x, z - 1);
            else if (z == 0)
                ActivateBlock(chunk.Neighbors.ZPos, y, x, last);
        }

        void ActivateBlock(Chunk chunk, int y, int x, int z)
        {
            if (chunk[x, y, z] is null)
            {
                return;
            }

            chunk.AddIndex(new(x, y, z));

            chunk.UpdateMesh = true;
        }

        void AdjustIndices(char face, Vector3 vector)
        {
            Vector3 zNeg = position + new Vector3(0, 0, -size),
                    zPos = position + new Vector3(0, 0, size),
                    xNeg = position + new Vector3(-size, 0, 0),
                    xPos = position + new Vector3(size, 0, 0);


            switch (face)
            {
                case 'X':
                    if (vector.X > 0) x--;
                    else x++;

                    if (x > last)
                    {
                        position = xNeg;
                        x = 0;
                    }

                    else if (x < 0)
                    {
                        position = xPos;
                        x = last;
                    }

                    break;

                case 'Y':
                    if (vector.Y > 0) y--;
                    else y++;

                    break;

                case 'Z':
                    if (vector.Z > 0) z--;
                    else z++;

                    if (z > last)
                    {
                        position = zNeg;
                        z = 0;
                    }

                    else if (z < 0)
                    {
                        position = zPos;
                        z = last;
                    }

                    break;
            }
        }
    }
}

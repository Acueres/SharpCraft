using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    class BlockHanlder
    {
        MainGame game;
        Player player;
        Dictionary<Vector3, Chunk> region;
        GameMenu gameMenu;
        DatabaseHandler databaseHandler;
        LightHandler lightHandler;

        int size, last;

        int x, y, z, index;
        Vector3 position;


        public BlockHanlder(MainGame _game, Player _player, Dictionary<Vector3, Chunk> _region,
            GameMenu _gameMenu, DatabaseHandler _databaseHandler, LightHandler _lightHandler, int _size)
        {
            game = _game;
            player = _player;
            region = _region;
            gameMenu = _gameMenu;
            databaseHandler = _databaseHandler;
            lightHandler = _lightHandler;

            size = _size;
            last = size - 1;
        }

        public void Reset()
        {
            y = -1;
        }

        public void Set(int _x, int _y, int _z, int _index, Vector3 _position)
        {
            x = _x;
            y = _y;
            z = _z;
            index = _index;
            position = _position;
        }

        public void Update(BlockSelector blockSelector, ChunkHandler chunkHandler)
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

            if (y != -1 && region[position].Blocks[y][x][z] != null)
            {
                bool[] visibleFaces = new bool[6];
                chunkHandler.GetVisibleFaces(visibleFaces, region[position], y, x, z);

                blockSelector.Update(visibleFaces, new Vector3(x, y, z) - position);
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

            chunk.Blocks[y][x][z] = null;
            
            chunk.ActiveY.RemoveAt(index);
            chunk.ActiveX.RemoveAt(index);
            chunk.ActiveZ.RemoveAt(index);

            lightHandler.Update(chunk, y, x, z, null);

            databaseHandler.AddDelta(position, y, x, z, null);

            UpdateAdjacentBlocks(chunk, y, x, z);
        }

        void AddBlock()
        {
            if (y == -1)
            {
                return;
            }

            Vector3 blockCenterDirection = (new Vector3(x, y, z) - position) - player.Position;
            blockCenterDirection.Normalize();

            char side = MaxVectorComponent(blockCenterDirection);

            AdjustIndices(side, blockCenterDirection);

            Vector3 blockPosition = new Vector3(x, y, z) - position;

            if ((blockPosition - player.Position).Length() < 1.1f)
            {
                return;
            }

            Chunk chunk = region[position];
            ushort? texture;

            if (chunk.Blocks[y][x][z] is null && gameMenu.SelectedItem != null)
            {
                texture = gameMenu.SelectedItem;
            }
            else
            {
                return;
            }

            chunk.Blocks[y][x][z] = texture;

            lightHandler.Update(chunk, y, x, z, texture);

            databaseHandler.AddDelta(position, y, x, z, texture);

            UpdateAdjacentBlocks(chunk, y, x, z);
        }

        void UpdateAdjacentBlocks(Chunk chunk, int y, int x, int z)
        {
            chunk.GenerateMesh = true;

            if (chunk.Blocks[y][x][z] != null)
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
            if (chunk.Blocks[y][x][z] is null)
            {
                return;
            }

            if (!IsBlockActive(chunk.ActiveY, chunk.ActiveX, chunk.ActiveZ, y, x, z))
            {
                chunk.ActiveY.Add((byte)y);
                chunk.ActiveX.Add((byte)x);
                chunk.ActiveZ.Add((byte)z);
            }

            chunk.GenerateMesh = true;
        }

        void AdjustIndices(char side, Vector3 vector)
        {
            Vector3 zNeg = position + new Vector3(0, 0, -size),
                    zPos = position + new Vector3(0, 0, size),
                    xNeg = position + new Vector3(-size, 0, 0),
                    xPos = position + new Vector3(size, 0, 0);


            switch (side)
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

        bool IsBlockActive(List<byte> arrayY, List<byte> arrayX, List<byte> arrayZ, int y, int x, int z)
        {
            for (int i = 0; i < arrayY.Count; i++)
            {
                if ((arrayY[i] == y) && (arrayX[i] == x) && (arrayZ[i] == z))
                {
                    return true;
                }
            }

            return false;
        }

        char MaxVectorComponent(Vector3 vector)
        {
            float max = Math.Abs(vector.X);
            char component = 'X';

            if (max < Math.Abs(vector.Y))
            {
                max = Math.Abs(vector.Y);
                component = 'Y';
            }

            if (max < Math.Abs(vector.Z))
            {
                component = 'Z';
            }

            return component;
        }
    }
}

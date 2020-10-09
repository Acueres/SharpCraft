using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SharpCraft
{
    class BlockHanlder
    {
        Player player;
        Dictionary<Vector3, Chunk> region;
        GameMenu gameMenu;
        int size;
        int x, y, z, index;
        Vector3 position;
        ushort bedrock, water;


        public BlockHanlder(Player _player, Dictionary<Vector3, Chunk> _region, 
            GameMenu _gameMenu, int _size, Dictionary<string, ushort> blockNames)
        {
            player = _player;
            region = _region;
            gameMenu = _gameMenu;
            size = _size;
            bedrock = blockNames["Bedrock"];
            water = blockNames["Water"];
        }

        public void Reset()
        {
            x = 0;
            y = 0;
            z = 0;
            index = 0;
            position = new Vector3(0, 0, 0);
        }

        public void Set(int _x, int _y, int _z, int _index, Vector3 _position)
        {
            x = _x;
            y = _y;
            z = _z;
            index = _index;
            position = _position;
        }

        public bool Update()
        {
            bool clickCondition = player.GameTime.TotalGameTime.TotalMilliseconds - player.LastClickTime > 100;

            if (clickCondition &&
                Util.IsLeftButtonClicked(player.CurrentMouseState, player.PreviousMouseState) &&
                !Parameters.ExitedMenu)
            {
                player.LastClickTime = player.GameTime.TotalGameTime.TotalMilliseconds;
                return RemoveBlock();
            }

            if (clickCondition &&
                Util.IsRightButtonClicked(player.CurrentMouseState, player.PreviousMouseState))
            {
                player.LastClickTime = player.GameTime.TotalGameTime.TotalMilliseconds;
                return AddBlock();
            }

            return false;
        }

        bool RemoveBlock()
        {
            if (//region[position].Blocks[y][x][z] == water ||
                region[position].Blocks[y][x][z] == bedrock)
            {
                return false;
            }

            region[position].Blocks[y][x][z] = null;

            region[position].ActiveY.RemoveAt(index);
            region[position].ActiveX.RemoveAt(index);
            region[position].ActiveZ.RemoveAt(index);

            UpdateAdjacentBlocks(region[position]);

            return true;
        }

        bool AddBlock()
        {
            Vector3 blockCenterDirection = (new Vector3(x, y, z) - position) - player.Position;
            blockCenterDirection.Normalize();

            char side = MaxVectorComponent(blockCenterDirection);

            AdjustIndices(side, blockCenterDirection);

            Vector3 blockPosition = new Vector3(x, y, z) - position;

            if ((blockPosition - player.Position).Length() < 1.1f)
            {
                return false;
            }

            Console.WriteLine(new Vector3(x, y, z));

            if (region[position].Blocks[y][x][z] is null && gameMenu.ActiveTool != null)
            {
                region[position].Blocks[y][x][z] = gameMenu.ActiveTool;
            }

            UpdateAdjacentBlocks(region[position]);

            return true;
        }

        void UpdateAdjacentBlocks(Chunk chunk)
        {
            Vector3 position = chunk.Position;

            chunk.UpdateMesh = true;

            if (!(chunk.Blocks[y][x][z] is null))
            {
                ActivateBlock(chunk, y, x, z);
            }

            ActivateBlock(chunk, y + 1, x, z);

            ActivateBlock(chunk, y - 1, x, z);

            Vector3 northPosition = position + new Vector3(0, 0, -size),
                    southPosition = position + new Vector3(0, 0, size),
                    eastPosition = position + new Vector3(-size, 0, 0),
                    westPosition = position + new Vector3(size, 0, 0);

            bool northChunkExists = region.ContainsKey(northPosition),
                 southChunkExists = region.ContainsKey(southPosition),
                 eastChunkExists = region.ContainsKey(eastPosition),
                 westChunkExists = region.ContainsKey(westPosition);

            if (x == size - 1 && eastChunkExists)
            {
                ActivateBlock(region[eastPosition], y, 0, z);

                ActivateBlock(chunk, y, x - 1, z);
            }

            else if (x == 0 && westChunkExists)
            {
                ActivateBlock(chunk, y, x + 1, z);

                ActivateBlock(region[westPosition], y, size - 1, z);
            }

            else
            {
                ActivateBlock(chunk, y, x + 1, z);

                ActivateBlock(chunk, y, x - 1, z);
            }


            if (z == size - 1 && northChunkExists)
            {
                ActivateBlock(region[northPosition], y, x, 0);

                ActivateBlock(chunk, y, x, z - 1);
            }

            else if (z == 0 && southChunkExists)
            {
                ActivateBlock(chunk, y, x, z + 1);

                ActivateBlock(region[southPosition], y, x, size - 1);
            }

            else
            {
                ActivateBlock(chunk, y, x, z + 1);

                ActivateBlock(chunk, y, x, z - 1);
            }
        }

        void ActivateBlock(Chunk chunk, int y, int x, int z)
        {
            try
            {
                if (chunk.Blocks[y][x][z] is null)
                {
                    return;
                }
            }

            catch (IndexOutOfRangeException)
            {
                return;
            }

            if (!IsBlockActive(chunk.ActiveY, chunk.ActiveX, chunk.ActiveZ, y, x, z))
            {
                chunk.ActiveY.Add((byte)y);
                chunk.ActiveX.Add((byte)x);
                chunk.ActiveZ.Add((byte)z);
            }

            chunk.UpdateMesh = true;
        }

        void AdjustIndices(char side, Vector3 vector)
        {

            Vector3 northPosition = position + new Vector3(0, 0, -size),
                    southPosition = position + new Vector3(0, 0, size),
                    eastPosition = position + new Vector3(-size, 0, 0),
                    westPosition = position + new Vector3(size, 0, 0);

            bool northChunkExists = region.ContainsKey(northPosition),
                 southChunkExists = region.ContainsKey(southPosition),
                 eastChunkExists = region.ContainsKey(eastPosition),
                 westChunkExists = region.ContainsKey(westPosition);

            switch (side)
            {
                case 'X':
                    if (vector.X > 0) x--;
                    else x++;

                    if (x > size - 1 && eastChunkExists)
                    {
                        position = eastPosition;
                        x = 0;
                    }

                    else if (x < 0 && westChunkExists)
                    {
                        position = westPosition;
                        x = size - 1;
                    }

                    break;

                case 'Y':
                    if (vector.Y > 0) y--;
                    else y++;

                    break;

                case 'Z':
                    if (vector.Z > 0) z--;
                    else z++;

                    if (z > size - 1 && northChunkExists)
                    {
                        position = northPosition;
                        z = 0;
                    }

                    else if (z < 0 && southChunkExists)
                    {
                        position = southPosition;
                        z = size - 1;
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

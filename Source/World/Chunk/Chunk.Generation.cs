using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

namespace SharpCraft.World
{
    public sealed partial class Chunk
    {
        public byte[][] BiomeData { get; }
        public List<Index> Active { get; }

        Dictionary<Vector3, Chunk> region;

        WorldGenerator worldGenerator;

        ushort?[][][] blocks;

        void Initialize()
        {
            worldGenerator.GenerateChunk(this);

            GetNeighbors();

            Active.Clear();

            bool[] visibleFaces = new bool[6];

            int height = blocks.Length;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        if (blocks[y][x][z] is null)
                        {
                            continue;
                        }

                        GetVisibleFaces(visibleFaces, y, x, z, calculateOpacity: false);

                        bool blockExposed = false;
                        for (int i = 0; i < visibleFaces.Length; i++)
                        {
                            if (visibleFaces[i])
                            {
                                blockExposed = true;
                                break;
                            }
                        }

                        if (blockExposed)
                        {
                            AddIndex(y, x, z);
                        }
                    }
                }
            }
        }

        public void GetVisibleFaces(bool[] visibleFaces, int y, int x, int z,
                                    bool calculateOpacity = true)
        {
            var transparent = Assets.TransparentBlocks;

            Array.Clear(visibleFaces, 0, 6);

            ushort? adjacentBlock;

            bool blockOpaque = true;
            if (calculateOpacity)
            {
                blockOpaque = !transparent[(int)blocks[y][x][z]];
            }

            if (y + 1 < height)
            {
                adjacentBlock = blocks[y + 1][x][z];
                visibleFaces[2] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);
            }

            if (y > 0)
            {
                adjacentBlock = blocks[y - 1][x][z];
                visibleFaces[3] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);
            }


            if (x == last)
            {
                if (Neighbors.XNeg != null)
                {
                    adjacentBlock = Neighbors.XNeg.blocks[y][0][z];
                }
                else
                {
                    Vector3 xNeg = Position + new Vector3(-size, 0, 0);
                    adjacentBlock = worldGenerator.Peek(xNeg, y, 0, z);
                }
            }
            else
            {
                adjacentBlock = blocks[y][x + 1][z];
            }

            visibleFaces[4] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);


            if (x == 0)
            {
                if (Neighbors.XPos != null)
                {
                    adjacentBlock = Neighbors.XPos.blocks[y][last][z];
                }
                else
                {
                    Vector3 xPos = Position + new Vector3(size, 0, 0);
                    adjacentBlock = worldGenerator.Peek(xPos, y, last, z);
                }
            }
            else
            {
                adjacentBlock = blocks[y][x - 1][z];
            }

            visibleFaces[5] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);


            if (z == last)
            {
                if (Neighbors.ZNeg != null)
                {
                    adjacentBlock = Neighbors.ZNeg.blocks[y][x][0];
                }
                else
                {
                    Vector3 zNeg = Position + new Vector3(0, 0, -size);
                    adjacentBlock = worldGenerator.Peek(zNeg, y, x, 0);
                }
            }
            else
            {
                adjacentBlock = blocks[y][x][z + 1];
            }

            visibleFaces[0] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);


            if (z == 0)
            {
                if (Neighbors.ZPos != null)
                {
                    adjacentBlock = Neighbors.ZPos.blocks[y][x][last];
                }
                else
                {
                    Vector3 zPos = Position + new Vector3(0, 0, size);
                    adjacentBlock = worldGenerator.Peek(zPos, y, x, last);
                }
            }
            else
            {
                adjacentBlock = blocks[y][x][z - 1];
            }

            visibleFaces[1] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpCraft
{
    class ChunkHandler
    {
        WorldGenerator worldGenerator;
        Dictionary<Vector3, Chunk> region;
        int size;
        int textureCount;
        Dictionary<ushort, ushort[]> multifaceBlocks;
        bool[] transparentBlocks;
        bool[] blockSpecial;
        Cube cube;


        public ChunkHandler(WorldGenerator _worldGenerator, Dictionary<Vector3, Chunk> _region,
            Dictionary<ushort, ushort[]> _multifaceBlocks,
            bool[] _transparentBlocks, int _size, int _textureCount)
        {
            cube = new Cube();

            worldGenerator = _worldGenerator;
            region = _region;
            multifaceBlocks = _multifaceBlocks;
            transparentBlocks = _transparentBlocks;
            size = _size;
            textureCount = _textureCount;

            blockSpecial = new bool[textureCount];

            for (ushort i = 0; i < blockSpecial.Length; i++)
            {
                if (multifaceBlocks.ContainsKey(i))
                {
                    blockSpecial[i] = true;
                }
            }
        }

        public void Initialize(Chunk chunk)
        {
            Vector3 position = chunk.Position;
            Vector3 northPosition = position + new Vector3(0, 0, -size),
                    southPosition = position + new Vector3(0, 0, size),
                    eastPosition = position + new Vector3(-size, 0, 0),
                    westPosition = position + new Vector3(size, 0, 0);

            bool northChunkExists = region.ContainsKey(northPosition) && region[northPosition] != null,
                 southChunkExists = region.ContainsKey(southPosition) && region[southPosition] != null,
                 eastChunkExists = region.ContainsKey(eastPosition) && region[eastPosition] != null,
                 westChunkExists = region.ContainsKey(westPosition) && region[westPosition] != null;

            chunk.ActiveY.Clear();
            chunk.ActiveX.Clear();
            chunk.ActiveZ.Clear();

            bool[] sideVisible = new bool[6];

            int height = chunk.Blocks.Length;
            int last = size - 1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        if (chunk.Blocks[y][x][z] is null)
                        {
                            continue;
                        }

                        for (int i = 0; i < sideVisible.Length; i++)
                        {
                            sideVisible[i] = false;
                        }

                        ushort? block;


                        if (y + 1 < height)
                        {
                            block = chunk.Blocks[y + 1][x][z];
                            sideVisible[2] = block is null || transparentBlocks[(int)block];
                        }

                        if (y > 0)
                        {
                            block = chunk.Blocks[y - 1][x][z];
                            sideVisible[3] = block is null || transparentBlocks[(int)block];
                        }


                        if (x == last && eastChunkExists)
                        {
                            block = region[eastPosition].Blocks[y][0][z];
                            sideVisible[4] = block is null || transparentBlocks[(int)block];

                            block = chunk.Blocks[y][x - 1][z];
                            sideVisible[5] = block is null || transparentBlocks[(int)block];
                        }

                        else if (x == last)
                        {
                            block = worldGenerator.LookUp(eastPosition, y, 0, z);
                            sideVisible[4] = block is null || transparentBlocks[(int)block];

                            block = chunk.Blocks[y][x - 1][z];
                            sideVisible[5] = block is null || transparentBlocks[(int)block];
                        }

                        else if (x == 0 && westChunkExists)
                        {
                            block = chunk.Blocks[y][x + 1][z];
                            sideVisible[4] = block is null || transparentBlocks[(int)block];

                            block = region[westPosition].Blocks[y][last][z];
                            sideVisible[5] = block is null || transparentBlocks[(int)block];
                        }

                        else if (x == 0)
                        {
                            block = chunk.Blocks[y][x + 1][z];
                            sideVisible[4] = block is null || transparentBlocks[(int)block];

                            block = worldGenerator.LookUp(westPosition, y, last, z);
                            sideVisible[5] = block is null || transparentBlocks[(int)block];
                        }

                        else if (x > 0 && x < last)
                        {
                            block = chunk.Blocks[y][x + 1][z];
                            sideVisible[4] = block is null || transparentBlocks[(int)block];

                            block = chunk.Blocks[y][x - 1][z];
                            sideVisible[5] = block is null || transparentBlocks[(int)block];
                        }


                        if (z == last && northChunkExists)
                        {
                            block = region[northPosition].Blocks[y][x][0];
                            sideVisible[0] = block is null || transparentBlocks[(int)block];

                            block = chunk.Blocks[y][x][z - 1];
                            sideVisible[1] = block is null || transparentBlocks[(int)block];
                        }

                        else if (z == last)
                        {
                            block = worldGenerator.LookUp(northPosition, y, x, 0);
                            sideVisible[0] = block is null || transparentBlocks[(int)block];

                            block = chunk.Blocks[y][x][z - 1];
                            sideVisible[1] = block is null || transparentBlocks[(int)block];
                        }

                        else if (z == 0 && southChunkExists)
                        {
                            block = chunk.Blocks[y][x][z + 1];
                            sideVisible[0] = block is null || transparentBlocks[(int)block];

                            block = region[southPosition].Blocks[y][x][last];
                            sideVisible[1] = block is null || transparentBlocks[(int)block];
                        }

                        else if (z == 0)
                        {
                            block = chunk.Blocks[y][x][z + 1];
                            sideVisible[0] = block is null || transparentBlocks[(int)block];

                            block = worldGenerator.LookUp(southPosition, y, x, last);
                            sideVisible[1] = block is null || transparentBlocks[(int)block];
                        }

                        else if (z > 0 && z < last)
                        {
                            block = chunk.Blocks[y][x][z + 1];
                            sideVisible[0] = block is null || transparentBlocks[(int)block];

                            block = chunk.Blocks[y][x][z - 1];
                            sideVisible[1] = block is null || transparentBlocks[(int)block];
                        }

                        bool isVisible = false;
                        for (int i = 0; i < sideVisible.Length; i++)
                        {
                            if (sideVisible[i])
                            {
                                isVisible = true;
                                break;
                            }
                        }

                        if (isVisible)
                        {
                            chunk.ActiveY.Add((byte)y);
                            chunk.ActiveX.Add((byte)x);
                            chunk.ActiveZ.Add((byte)z);
                        }
                    }
                }
            }
            chunk.Initialize = false;
        }

        public void GenerateMesh(Chunk chunk)
        {
            Vector3 position = chunk.Position;
            bool[] sideVisible = new bool[6];

            for (int i = 0; i < chunk.ActiveY.Count; i++)
            {
                byte y = chunk.ActiveY[i];
                byte x = chunk.ActiveX[i];
                byte z = chunk.ActiveZ[i];

                Vector3 blockPosition = new Vector3(x, y, z) - position;

                GetVisibleSides(sideVisible, chunk, y, x, z);

                for (int side = 0; side < 6; side++)
                {
                    if (sideVisible[side])
                    {
                        AddFaceMesh(chunk, chunk.Blocks[y][x][z], side, blockPosition);
                    }
                }
            }

            Clear(chunk);
            chunk.GenerateMesh = false;
        }

        public void GetVisibleSides(bool[] sideVisible, Chunk chunk, int y, int x, int z)
        {
            Vector3 position = chunk.Position;
            int height = chunk.Blocks.Length;
            int last = size - 1;

            for (int i = 0; i < sideVisible.Length; i++)
            {
                sideVisible[i] = false;
            }

            Vector3 northPosition = position + new Vector3(0, 0, -size),
                    southPosition = position + new Vector3(0, 0, size),
                    eastPosition = position + new Vector3(-size, 0, 0),
                    westPosition = position + new Vector3(size, 0, 0);

            bool northChunkExists = region.ContainsKey(northPosition) && region[northPosition] != null,
                 southChunkExists = region.ContainsKey(southPosition) && region[southPosition] != null,
                 eastChunkExists = region.ContainsKey(eastPosition) && region[eastPosition] != null,
                 westChunkExists = region.ContainsKey(westPosition) && region[westPosition] != null;

            bool isCurrentOpaque = !transparentBlocks[(int)chunk.Blocks[y][x][z]];

            ushort? block;

            if (y + 1 < height)
            {
                block = chunk.Blocks[y + 1][x][z];
                sideVisible[2] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            if (y > 0)
            {
                block = chunk.Blocks[y - 1][x][z];
                sideVisible[3] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            if (x == last && eastChunkExists)
            {
                block = region[eastPosition].Blocks[y][0][z];
                sideVisible[4] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = chunk.Blocks[y][x - 1][z];
                sideVisible[5] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            else if (x == last)
            {
                block = worldGenerator.LookUp(eastPosition, y, 0, z);
                sideVisible[4] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = chunk.Blocks[y][x - 1][z];
                sideVisible[5] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            else if (x == 0 && westChunkExists)
            {
                block = chunk.Blocks[y][x + 1][z];
                sideVisible[4] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = region[westPosition].Blocks[y][last][z];
                sideVisible[5] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            else if (x == 0)
            {
                block = chunk.Blocks[y][x + 1][z];
                sideVisible[4] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = worldGenerator.LookUp(westPosition, y, last, z);
                sideVisible[5] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            else if (x > 0 && x < last)
            {
                block = chunk.Blocks[y][x + 1][z];
                sideVisible[4] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = chunk.Blocks[y][x - 1][z];
                sideVisible[5] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }


            if (z == last && northChunkExists)
            {
                block = region[northPosition].Blocks[y][x][0];
                sideVisible[0] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = chunk.Blocks[y][x][z - 1];
                sideVisible[1] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            else if (z == last)
            {
                block = worldGenerator.LookUp(northPosition, y, x, 0);
                sideVisible[0] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = chunk.Blocks[y][x][z - 1];
                sideVisible[1] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            else if (z == 0 && southChunkExists)
            {
                block = chunk.Blocks[y][x][z + 1];
                sideVisible[0] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = region[southPosition].Blocks[y][x][last];
                sideVisible[1] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            else if (z == 0)
            {
                block = chunk.Blocks[y][x][z + 1];
                sideVisible[0] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = worldGenerator.LookUp(southPosition, y, x, last);
                sideVisible[1] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }

            else if (z > 0 && z < last)
            {
                block = chunk.Blocks[y][x][z + 1];
                sideVisible[0] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);

                block = chunk.Blocks[y][x][z - 1];
                sideVisible[1] = block is null || (transparentBlocks[(int)block] && isCurrentOpaque);
            }
        }

        void Clear(Chunk chunk)
        {
            chunk.Vertices = chunk.VertexList.ToArray();
            chunk.TransparentVertices = chunk.TransparentVertexList.ToArray();

            chunk.VertexCount = chunk.VertexList.Count;
            chunk.TransparentVertexCount = chunk.TransparentVertexList.Count;

            chunk.VertexList.Clear();
            chunk.TransparentVertexList.Clear();
        }

        void AddFaceMesh(Chunk chunk, ushort? texture, int side, Vector3 blockPosition)
        {
            if (blockSpecial[(int)texture])
            {
                AddData(chunk.VertexList,
                    side, blockPosition, multifaceBlocks[(ushort)texture][side]);
            }
            else if (transparentBlocks[(int)texture])
            {
                AddData(chunk.TransparentVertexList, side, blockPosition, texture);
            }
            else
            {
                AddData(chunk.VertexList, side, blockPosition, texture);
            }
        }

        void AddData(List<VertexPositionNormalTexture> vertices, int side, Vector3 position, ushort? texture)
        {
            for (int i = 0; i < 6; i++)
            {
                VertexPositionNormalTexture vertex = cube.Faces[side][i];
                vertex.Position += position;

                if (vertex.TextureCoordinate.Y == 0)
                {
                    vertex.TextureCoordinate.Y = (float)texture / textureCount;
                }
                else
                {
                    vertex.TextureCoordinate.Y = (float)(texture + 1) / textureCount;
                }
                vertices.Add(vertex);
            }
        }
    }
}

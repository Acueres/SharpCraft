using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    class ChunkHandler
    {
        WorldGenerator worldGenerator;
        Cube cube;
        Dictionary<Vector3, Chunk> region;

        int size;
        int height;
        int last;

        int textureCount;

        ReadOnlyDictionary<ushort, ushort[]> multifaceBlocks;

        IList<bool> transparent;
        bool[] multiface;


        public ChunkHandler(WorldGenerator _worldGenerator, Dictionary<Vector3, Chunk> _region, Parameters parameters)
        {
            worldGenerator = _worldGenerator;
            region = _region;
            cube = new Cube();

            size = Settings.ChunkSize;
            height = 128;
            last = size - 1;

            textureCount = Assets.BlockTextures.Count;

            multifaceBlocks = Assets.MultifaceBlocks;

            transparent = Assets.TransparentBlocks;
            multiface = new bool[textureCount];
            for (ushort i = 0; i < multiface.Length; i++)
            {
                if (multifaceBlocks.ContainsKey(i))
                {
                    multiface[i] = true;
                }
            }
        }

        public void Initialize(Chunk chunk)
        {
            GetNeighbors(chunk);

            chunk.ActiveY.Clear();
            chunk.ActiveX.Clear();
            chunk.ActiveZ.Clear();

            bool[] visibleFaces = new bool[6];

            int height = chunk.Blocks.Length;

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

                        GetVisibleFaces(visibleFaces, chunk, y, x, z, calculateOpacity: false);

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
                            chunk.ActiveY.Add((byte)y);
                            chunk.ActiveX.Add((byte)x);
                            chunk.ActiveZ.Add((byte)z);
                        }
                    }
                }
            }

            chunk.Initialize = false;
        }

        public void Dereference(Chunk chunk)
        {
            if (chunk.Neighbors.ZNeg != null)
            {
                chunk.Neighbors.ZNeg.Neighbors.ZPos = null;
            }

            if (chunk.Neighbors.ZPos != null)
            {
                chunk.Neighbors.ZPos.Neighbors.ZNeg = null;
            }

            if (chunk.Neighbors.XNeg != null)
            {
                chunk.Neighbors.XNeg.Neighbors.XPos = null;
            }

            if (chunk.Neighbors.XPos != null)
            {
                chunk.Neighbors.XPos.Neighbors.XNeg = null;
            }
        }

        public void GenerateMesh(Chunk chunk)
        {
            Vector3 position = chunk.Position;
            bool[] visibleFaces = new bool[6];
            byte[] lightValues = new byte[6];

            for (int i = 0; i < chunk.ActiveY.Count; i++)
            {
                byte y = chunk.ActiveY[i];
                byte x = chunk.ActiveX[i];
                byte z = chunk.ActiveZ[i];

                Vector3 blockPosition = new Vector3(x, y, z) - position;

                GetVisibleFaces(visibleFaces, chunk, y, x, z);
                GetFacesLight(lightValues, visibleFaces, chunk, y, x, z);

                for (int face = 0; face < 6; face++)
                {
                    if (visibleFaces[face])
                    {
                        AddFaceMesh(chunk, chunk.Blocks[y][x][z], face, lightValues[face], blockPosition);
                    }
                }
            }

            Clear(chunk);
            chunk.GenerateMesh = false;
        }

        public void GetVisibleFaces(bool[] visibleFaces, Chunk chunk, int y, int x, int z,
                                    bool calculateOpacity = true)
        {
            Array.Clear(visibleFaces, 0, 6);

            ushort? adjacentBlock;

            bool blockOpaque = true;
            if (calculateOpacity)
            {
                blockOpaque = !transparent[(int)chunk.Blocks[y][x][z]];
            }

            if (y + 1 < height)
            {
                adjacentBlock = chunk.Blocks[y + 1][x][z];
                visibleFaces[2] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);
            }

            if (y > 0)
            {
                adjacentBlock = chunk.Blocks[y - 1][x][z];
                visibleFaces[3] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);
            }


            if (x == last)
            {
                if (chunk.Neighbors.XNeg != null)
                {
                    adjacentBlock = chunk.Neighbors.XNeg.Blocks[y][0][z];
                }
                else
                {
                    Vector3 xNeg = chunk.Position + new Vector3(-size, 0, 0);
                    adjacentBlock = worldGenerator.Peek(xNeg, y, 0, z);
                }
            }
            else
            {
                adjacentBlock = chunk.Blocks[y][x + 1][z];
            }

            visibleFaces[4] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);


            if (x == 0)
            {
                if (chunk.Neighbors.XPos != null)
                {
                    adjacentBlock = chunk.Neighbors.XPos.Blocks[y][last][z];
                }
                else
                {
                    Vector3 xPos = chunk.Position + new Vector3(size, 0, 0);
                    adjacentBlock = worldGenerator.Peek(xPos, y, last, z);
                }
            }
            else
            {
                adjacentBlock = chunk.Blocks[y][x - 1][z];
            }

            visibleFaces[5] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);


            if (z == last)
            {
                if (chunk.Neighbors.ZNeg != null)
                {
                    adjacentBlock = chunk.Neighbors.ZNeg.Blocks[y][x][0];
                }
                else
                {
                    Vector3 zNeg = chunk.Position + new Vector3(0, 0, -size);
                    adjacentBlock = worldGenerator.Peek(zNeg, y, x, 0);
                }
            }
            else
            {
                adjacentBlock = chunk.Blocks[y][x][z + 1];
            }

            visibleFaces[0] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);


            if (z == 0)
            {
                if (chunk.Neighbors.ZPos != null)
                {
                    adjacentBlock = chunk.Neighbors.ZPos.Blocks[y][x][last];
                }
                else
                {
                    Vector3 zPos = chunk.Position + new Vector3(0, 0, size);
                    adjacentBlock = worldGenerator.Peek(zPos, y, x, last);
                }
            }
            else
            {
                adjacentBlock = chunk.Blocks[y][x][z - 1];
            }

            visibleFaces[1] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);
        }

        void GetFacesLight(byte[] lightValues, bool[] facesVisible, Chunk chunk, int y, int x, int z)
        {
            if (facesVisible[2])
            {
                lightValues[2] = chunk.LightMap[y + 1][x][z];
            }

            if (facesVisible[3])
            {
                lightValues[3] = chunk.LightMap[y - 1][x][z];
            }


            if (facesVisible[4])
            {
                if (x == last)
                {
                    if (chunk.Neighbors.XNeg != null)
                        lightValues[4] = chunk.Neighbors.XNeg.LightMap[y][0][z];
                }
                else
                {
                    lightValues[4] = chunk.LightMap[y][x + 1][z];
                }
            }

            if (facesVisible[5])
            {
                if (x == 0)
                {
                    if (chunk.Neighbors.XPos != null)
                        lightValues[5] = chunk.Neighbors.XPos.LightMap[y][last][z];
                }
                else
                {
                    lightValues[5] = chunk.LightMap[y][x - 1][z];
                }
            }


            if (facesVisible[0])
            {
                if (z == last)
                {
                    if (chunk.Neighbors.ZNeg != null)
                        lightValues[0] = chunk.Neighbors.ZNeg.LightMap[y][x][0];
                }
                else
                {
                    lightValues[0] = chunk.LightMap[y][x][z + 1];
                }
            }

            if (facesVisible[1])
            {
                if (z == 0)
                {
                    if (chunk.Neighbors.ZPos != null)
                        lightValues[1] = chunk.Neighbors.ZPos.LightMap[y][x][last];
                }
                else
                {
                    lightValues[1] = chunk.LightMap[y][x][z - 1];
                }
            }
        }

        void GetNeighbors(Chunk chunk)
        {
            Vector3 position = chunk.Position;

            Vector3 zNegPosition = position + new Vector3(0, 0, -size),
                    zPosPosition = position + new Vector3(0, 0, size),
                    xNegPosition = position + new Vector3(-size, 0, 0),
                    xPosPosition = position + new Vector3(size, 0, 0);

            chunk.Neighbors.ZNeg = region.ContainsKey(zNegPosition) ? region[zNegPosition] : null;
            if (chunk.Neighbors.ZNeg != null && chunk.Neighbors.ZNeg.Neighbors.ZPos is null)
            {
                chunk.Neighbors.ZNeg.Neighbors.ZPos = chunk;
            }

            chunk.Neighbors.ZPos = region.ContainsKey(zPosPosition) ? region[zPosPosition] : null;
            if (chunk.Neighbors.ZPos != null && chunk.Neighbors.ZPos.Neighbors.ZNeg is null)
            {
                chunk.Neighbors.ZPos.Neighbors.ZNeg = chunk;
            }

            chunk.Neighbors.XNeg = region.ContainsKey(xNegPosition) ? region[xNegPosition] : null;
            if (chunk.Neighbors.XNeg != null && chunk.Neighbors.XNeg.Neighbors.XPos is null)
            {
                chunk.Neighbors.XNeg.Neighbors.XPos = chunk;
            }

            chunk.Neighbors.XPos = region.ContainsKey(xPosPosition) ? region[xPosPosition] : null;
            if (chunk.Neighbors.XPos != null && chunk.Neighbors.XPos.Neighbors.XNeg is null)
            {
                chunk.Neighbors.XPos.Neighbors.XNeg = chunk;
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

        void AddFaceMesh(Chunk chunk, ushort? texture, int face, byte light, Vector3 blockPosition)
        {
            if (multiface[(int)texture])
            {
                AddData(chunk.VertexList,
                    face, light, blockPosition, multifaceBlocks[(ushort)texture][face]);
            }
            else if (transparent[(int)texture])
            {
                AddData(chunk.TransparentVertexList, face, light, blockPosition, texture);
            }
            else
            {
                AddData(chunk.VertexList, face, light, blockPosition, texture);
            }
        }

        void AddData(List<VertexPositionTextureLight> vertices, int face, float light, Vector3 position, ushort? texture)
        {
            for (int i = 0; i < 6; i++)
            {
                VertexPositionTextureLight vertex = cube.Faces[face][i];
                vertex.Position += position;
                vertex.Light = light;

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

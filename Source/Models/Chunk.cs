using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;

using SharpCraft.Rendering;
using SharpCraft.Models;


namespace SharpCraft.World
{
    public class Chunk : IDisposable
    {
        public Vector3 Position;

        public Dictionary<Vector3, Chunk> region;

        WorldGenerator worldGenerator;

        public NeighborChunks Neighbors;

        public bool GenerateMesh;
        public bool Initialize;

        public ushort?[][][] Blocks;

        public byte[][] BiomeData;

        public byte[][][] LightMap;

        public List<Index> Active;
        public List<Index> LightSources;

        public VertexPositionTextureLight[] Vertices;
        public VertexPositionTextureLight[] TransparentVertices;

        public List<VertexPositionTextureLight> VertexList;
        public List<VertexPositionTextureLight> TransparentVertexList;

        public int VertexCount;
        public int TransparentVertexCount;

        public void Dispose() => Dispose(true);
        SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;

        public class NeighborChunks
        {
            public Chunk ZNeg, ZPos, XNeg, XPos;
        }

        public struct Index
        {
            int index;

            const int size = 16;
            const int size2 = 16 * 16;

            public int X
            {
                get
                {
                    return index % size;
                }
            }

            public int Y
            {
                get
                {
                    return index / size2;
                }
            }

            public int Z
            {
                get
                {
                    return (index % size2) / size;
                }
            }


            public Index(int y, int x, int z)
            {
                index = x + z * size + y * size2;
            }
        }


        public Chunk(Vector3 position, WorldGenerator worldGenerator, Dictionary<Vector3, Chunk> region, int size = 16, int height = 128)
        {
            Position = position;

            Neighbors = new NeighborChunks();

            this.region = region;
            this.worldGenerator = worldGenerator;

            GenerateMesh = true;
            Initialize = true;

            //Only about ~5% of all blocks are visible
            int total = (int)(0.05 * size * size * height);

            Blocks = new ushort?[height][][];
            for (int y = 0; y < height; y++)
            {
                Blocks[y] = new ushort?[size][];

                for (int x = 0; x < size; x++)
                {
                    Blocks[y][x] = new ushort?[size];
                }
            }

            BiomeData = new byte[size][];
            for (int x = 0; x < BiomeData.Length; x++)
            {
                BiomeData[x] = new byte[size];
            }

            LightMap = new byte[height][][];
            for (int y = 0; y < height; y++)
            {
                LightMap[y] = new byte[size][];

                for (int x = 0; x < size; x++)
                {
                    LightMap[y][x] = new byte[size];
                }
            }

            Active = new List<Index>(total);
            LightSources = new List<Index>(100);

            VertexList = new List<VertexPositionTextureLight>(6 * total);
            TransparentVertexList = new List<VertexPositionTextureLight>(3 * total);
        }

        public void Init()
        {
            int size = 16;
            GetNeighbors();

            Active.Clear();

            bool[] visibleFaces = new bool[6];

            int height = Blocks.Length;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        if (Blocks[y][x][z] is null)
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

            Initialize = false;
        }

        public void GetVisibleFaces(bool[] visibleFaces, int y, int x, int z,
                                    bool calculateOpacity = true)
        {
            int size = 16;
            int last = size - 1;
            int height = 128;
            var transparent = Assets.TransparentBlocks;

            Array.Clear(visibleFaces, 0, 6);

            ushort? adjacentBlock;

            bool blockOpaque = true;
            if (calculateOpacity)
            {
                blockOpaque = !transparent[(int)Blocks[y][x][z]];
            }

            if (y + 1 < height)
            {
                adjacentBlock = Blocks[y + 1][x][z];
                visibleFaces[2] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);
            }

            if (y > 0)
            {
                adjacentBlock = Blocks[y - 1][x][z];
                visibleFaces[3] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);
            }


            if (x == last)
            {
                if (Neighbors.XNeg != null)
                {
                    adjacentBlock = Neighbors.XNeg.Blocks[y][0][z];
                }
                else
                {
                    Vector3 xNeg = Position + new Vector3(-size, 0, 0);
                    adjacentBlock = worldGenerator.Peek(xNeg, y, 0, z);
                }
            }
            else
            {
                adjacentBlock = Blocks[y][x + 1][z];
            }

            visibleFaces[4] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);


            if (x == 0)
            {
                if (Neighbors.XPos != null)
                {
                    adjacentBlock = Neighbors.XPos.Blocks[y][last][z];
                }
                else
                {
                    Vector3 xPos = Position + new Vector3(size, 0, 0);
                    adjacentBlock = worldGenerator.Peek(xPos, y, last, z);
                }
            }
            else
            {
                adjacentBlock = Blocks[y][x - 1][z];
            }

            visibleFaces[5] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);


            if (z == last)
            {
                if (Neighbors.ZNeg != null)
                {
                    adjacentBlock = Neighbors.ZNeg.Blocks[y][x][0];
                }
                else
                {
                    Vector3 zNeg = Position + new Vector3(0, 0, -size);
                    adjacentBlock = worldGenerator.Peek(zNeg, y, x, 0);
                }
            }
            else
            {
                adjacentBlock = Blocks[y][x][z + 1];
            }

            visibleFaces[0] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);


            if (z == 0)
            {
                if (Neighbors.ZPos != null)
                {
                    adjacentBlock = Neighbors.ZPos.Blocks[y][x][last];
                }
                else
                {
                    Vector3 zPos = Position + new Vector3(0, 0, size);
                    adjacentBlock = worldGenerator.Peek(zPos, y, x, last);
                }
            }
            else
            {
                adjacentBlock = Blocks[y][x][z - 1];
            }

            visibleFaces[1] = adjacentBlock is null || (transparent[(int)adjacentBlock] && blockOpaque);
        }

        public void Dereference()
        {
            if (Neighbors.ZNeg != null)
            {
                Neighbors.ZNeg.Neighbors.ZPos = null;
            }

            if (Neighbors.ZPos != null)
            {
                Neighbors.ZPos.Neighbors.ZNeg = null;
            }

            if (Neighbors.XNeg != null)
            {
                Neighbors.XNeg.Neighbors.XPos = null;
            }

            if (Neighbors.XPos != null)
            {
                Neighbors.XPos.Neighbors.XNeg = null;
            }
        }

        void GetNeighbors()
        {
            int size = 16;
            Vector3 zNegPosition = Position + new Vector3(0, 0, -size),
                    zPosPosition = Position + new Vector3(0, 0, size),
                    xNegPosition = Position + new Vector3(-size, 0, 0),
                    xPosPosition = Position + new Vector3(size, 0, 0);

            Neighbors.ZNeg = region.ContainsKey(zNegPosition) ? region[zNegPosition] : null;
            if (Neighbors.ZNeg != null && Neighbors.ZNeg.Neighbors.ZPos is null)
            {
                Neighbors.ZNeg.Neighbors.ZPos = this;
            }

            Neighbors.ZPos = region.ContainsKey(zPosPosition) ? region[zPosPosition] : null;
            if (Neighbors.ZPos != null && Neighbors.ZPos.Neighbors.ZNeg is null)
            {
                Neighbors.ZPos.Neighbors.ZNeg = this;
            }

            Neighbors.XNeg = region.ContainsKey(xNegPosition) ? region[xNegPosition] : null;
            if (Neighbors.XNeg != null && Neighbors.XNeg.Neighbors.XPos is null)
            {
                Neighbors.XNeg.Neighbors.XPos = this;
            }

            Neighbors.XPos = region.ContainsKey(xPosPosition) ? region[xPosPosition] : null;
            if (Neighbors.XPos != null && Neighbors.XPos.Neighbors.XNeg is null)
            {
                Neighbors.XPos.Neighbors.XNeg = this;
            }
        }

        public void MakeMesh()
        {
            bool[] visibleFaces = new bool[6];
            byte[] lightValues = new byte[6];

            for (int i = 0; i < Active.Count; i++)
            {
                int y = Active[i].Y;
                int x = Active[i].X;
                int z = Active[i].Z;

                Vector3 blockPosition = new Vector3(x, y, z) - Position;

                GetVisibleFaces(visibleFaces, y, x, z);
                GetFacesLight(lightValues, visibleFaces, y, x, z);

                for (int face = 0; face < 6; face++)
                {
                    if (visibleFaces[face])
                    {
                        AddFaceMesh(Blocks[y][x][z], face, lightValues[face], blockPosition);
                    }
                }
            }

            UpdateMesh();
        }

        void AddFaceMesh(ushort? texture, int face, byte light, Vector3 blockPosition)
        {
            var multiface = Assets.Multiface;
            var multifaceBlocks = Assets.MultifaceBlocks;
            var transparent = Assets.TransparentBlocks;

            if (multiface[(int)texture])
            {
                AddData(VertexList,
                    face, light, blockPosition, multifaceBlocks[(ushort)texture][face]);
            }
            else if (transparent[(int)texture])
            {
                AddData(TransparentVertexList, face, light, blockPosition, texture);
            }
            else
            {
                AddData(VertexList, face, light, blockPosition, texture);
            }
        }

        void AddData(List<VertexPositionTextureLight> vertices, int face, byte light, Vector3 position, ushort? texture)
        {
            int size = 16;
            int textureCount = Assets.BlockTextures.Count;
            for (int i = 0; i < 6; i++)
            {
                VertexPositionTextureLight vertex = Cube.Faces[face][i];
                vertex.Position += position;

                int skylight = (light >> 4) & 0xF;
                int blockLight = light & 0xF;

                vertex.Light = skylight + size * blockLight;

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

        void GetFacesLight(byte[] lightValues, bool[] facesVisible, int y, int x, int z)
        {
            int size = 16;
            int last = size - 1;
            if (facesVisible[2])
            {
                lightValues[2] = LightMap[y + 1][x][z];
            }

            if (facesVisible[3])
            {
                lightValues[3] = LightMap[y - 1][x][z];
            }


            if (facesVisible[4])
            {
                if (x == last)
                {
                    if (Neighbors.XNeg != null)
                        lightValues[4] = Neighbors.XNeg.LightMap[y][0][z];
                }
                else
                {
                    lightValues[4] = LightMap[y][x + 1][z];
                }
            }

            if (facesVisible[5])
            {
                if (x == 0)
                {
                    if (Neighbors.XPos != null)
                        lightValues[5] = Neighbors.XPos.LightMap[y][last][z];
                }
                else
                {
                    lightValues[5] = LightMap[y][x - 1][z];
                }
            }


            if (facesVisible[0])
            {
                if (z == last)
                {
                    if (Neighbors.ZNeg != null)
                        lightValues[0] = Neighbors.ZNeg.LightMap[y][x][0];
                }
                else
                {
                    lightValues[0] = LightMap[y][x][z + 1];
                }
            }

            if (facesVisible[1])
            {
                if (z == 0)
                {
                    if (Neighbors.ZPos != null)
                        lightValues[1] = Neighbors.ZPos.LightMap[y][x][last];
                }
                else
                {
                    lightValues[1] = LightMap[y][x][z - 1];
                }
            }
        }

        public void UpdateMesh()
        {
            Vertices = VertexList.ToArray();
            TransparentVertices = TransparentVertexList.ToArray();

            VertexCount = VertexList.Count;
            TransparentVertexCount = TransparentVertexList.Count;

            VertexList.Clear();
            TransparentVertexList.Clear();

            GenerateMesh = false;
        }

        public void AddIndex(int y, int x, int z)
        {
            if (!Active.Contains(new Index(y, x, z))) 
            {
                Active.Add(new Index(y, x, z));
            }
        }

        public void AddLightSource(int y, int x, int z)
        {
            LightSources.Add(new Index(y, x, z));
        }

        public void SetLight(int y, int x, int z, byte value, bool skylight)
        {
            if (skylight)
            {
                LightMap[y][x][z] = (byte)((LightMap[y][x][z] & 0xF) | (value << 4));
            }
            else
            {
                LightMap[y][x][z] = (byte)((LightMap[y][x][z] & 0xF0) | value);
            }
        }

        public byte GetLight(int y, int x, int z, bool skylight)
        {
            if (skylight)
            {
                return (byte)((LightMap[y][x][z] >> 4) & 0xF);
            }
            else
            {
                return (byte)(LightMap[y][x][z] & 0xF);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                Dereference();
                safeHandle?.Dispose();
            }

            disposed = true;
        }
    }
}

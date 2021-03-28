using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;

using SharpCraft.Rendering;


namespace SharpCraft.World
{
    public class Chunk : IDisposable
    {
        public Vector3 Position;

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


        public Chunk(Vector3 position, int size = 16, int height = 128)
        {
            Position = position;

            Neighbors = new NeighborChunks();

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
                safeHandle?.Dispose();
            }

            disposed = true;
        }
    }
}

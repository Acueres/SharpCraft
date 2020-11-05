using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    public class Chunk : IDisposable
    {
        public Vector3 Position;

        public NeighboringChunks Neighbors;

        public bool GenerateMesh;
        public bool Initialize;
        public bool CalculateLight;

        public ushort?[][][] Blocks;

        public byte[][] BiomeData;

        public byte[][][] LightMap;

        public List<byte> ActiveY;
        public List<byte> ActiveX;
        public List<byte> ActiveZ;

        public VertexPositionTextureLight[] Vertices;
        public VertexPositionTextureLight[] TransparentVertices;

        public List<VertexPositionTextureLight> VertexList;
        public List<VertexPositionTextureLight> TransparentVertexList;

        public int VertexCount;
        public int TransparentVertexCount;

        public void Dispose() => Dispose(true);
        SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;


        public Chunk(Vector3 position, int size = 16, int height = 128)
        {
            Position = position;

            Neighbors = new NeighboringChunks();

            GenerateMesh = true;
            Initialize = true;
            CalculateLight = true;

            //Only about <5% of all blocks in a chunk are visible
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

            //Set the topmost layer to max light value
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    LightMap[height - 1][x][z] = 16;
                }
            }

            ActiveY = new List<byte>(total);
            ActiveX = new List<byte>(total);
            ActiveZ = new List<byte>(total);

            VertexList = new List<VertexPositionTextureLight>(6 * total);
            TransparentVertexList = new List<VertexPositionTextureLight>(3 * total);
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

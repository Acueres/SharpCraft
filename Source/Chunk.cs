using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;


namespace SharpCraft
{
    public class Chunk : IDisposable
    {
        public Vector3 Position;

        public bool GenerateMesh;
        public bool Initialize;

        public ushort?[][][] Blocks;

        public byte[][] BiomeData;

        public List<byte> ActiveY;
        public List<byte> ActiveX;
        public List<byte> ActiveZ;

        public VertexPositionNormalTexture[] Vertices;
        public VertexPositionNormalTexture[] TransparentVertices;

        public List<VertexPositionNormalTexture> VertexList;
        public List<VertexPositionNormalTexture> TransparentVertexList;

        public int VertexCount;
        public int TransparentVertexCount;

        public void Dispose() => Dispose(true);
        SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;


        public Chunk(Vector3 position, int size = 16, int height = 128)
        {
            Position = position;

            GenerateMesh = true;
            Initialize = true;

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

            //Only about <5% of all blocks in a chunk are visible
            int total = (int)(0.05 * size * size * height);

            ActiveY = new List<byte>(total);
            ActiveX = new List<byte>(total);
            ActiveZ = new List<byte>(total);

            VertexList = new List<VertexPositionNormalTexture>(6 * total);
            TransparentVertexList = new List<VertexPositionNormalTexture>(3 * total);
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

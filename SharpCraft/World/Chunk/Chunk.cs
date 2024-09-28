using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;
using Microsoft.Xna.Framework;

using SharpCraft.Rendering;
using System.Linq;
using SharpCraft.Utility;


namespace SharpCraft.World
{
    public sealed partial class Chunk : IDisposable
    {
        public Vector3I Index { get; }
        public Vector3 Position { get; }

        public byte[][] BiomeData { get; }
        readonly HashSet<Vector3I> activeBlockIndexes;

        readonly Block[][][] blocks;

        public void Dispose() => Dispose(true);
        readonly SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;

        public const int SIZE = 16;
        public const int HEIGHT = 128;
        public const int LAST = SIZE - 1;

        readonly BlockMetadataProvider blockMetadata;

        public Chunk(Vector3I position, BlockMetadataProvider blockMetadata)
        {
            Index = position;
            Position = SIZE * new Vector3(position.X, position.Y, position.Z);

            this.blockMetadata = blockMetadata;

            //Only about ~5% of all blocks are visible
            int total = (int)(0.05 * SIZE * SIZE * HEIGHT);

            blocks = new Block[HEIGHT][][];
            for (int y = 0; y < HEIGHT; y++)
            {
                blocks[y] = new Block[SIZE][];

                for (int x = 0; x < SIZE; x++)
                {
                    blocks[y][x] = new Block[SIZE];
                }
            }

            BiomeData = new byte[SIZE][];
            for (int x = 0; x < BiomeData.Length; x++)
            {
                BiomeData[x] = new byte[SIZE];
            }

            activeBlockIndexes = new HashSet<Vector3I>(total);

            //Initialize mesh
            VertexList = new List<VertexPositionTextureLight>(6 * total);
            TransparentVertexList = new List<VertexPositionTextureLight>(3 * total);

            //Initialize light
            lightQueue = new Queue<LightNode>(100);
            lightList = new List<LightNode>(100);

            chunksToUpdate = new HashSet<Chunk>(5);

            lightMap = new byte[HEIGHT][][];
            for (int y = 0; y < HEIGHT; y++)
            {
                lightMap[y] = new byte[SIZE][];

                for (int x = 0; x < SIZE; x++)
                {
                    lightMap[y][x] = new byte[SIZE];
                }
            }

            lightSourceIndexes = [];
        }

        public Block this[int x, int y, int z]
        {
            get => blocks[y][x][z];
            set => blocks[y][x][z] = value;
        }       

        public bool AddIndex(Vector3I index)
        {
            return activeBlockIndexes.Add(index);
        }

        public bool RemoveIndex(Vector3I index)
        {
            return activeBlockIndexes.Remove(index);
        }

        public Vector3I GetIndex(int i)
        {
            return activeBlockIndexes.Where((index, id) => id == i).Single();
        }

        public IEnumerable<Vector3I> GetIndexes()
        {
            foreach (Vector3I index in activeBlockIndexes)
            {
                yield return index;
            }
        }

        void Dispose(bool disposing)
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

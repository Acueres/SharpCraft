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

        public BiomeType[][] Biomes { get; }
        readonly HashSet<Vector3I> activeBlockIndexes;

        readonly Block[][][] blocks;

        public void Dispose() => Dispose(true);
        readonly SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;

        public const int Size = 16;
        public const int Height = 128;
        public const int Last = Size - 1;

        readonly BlockMetadataProvider blockMetadata;

        public Chunk(Vector3I position, BlockMetadataProvider blockMetadata)
        {
            Index = position;
            Position = Size * new Vector3(position.X, position.Y, position.Z);

            this.blockMetadata = blockMetadata;

            //Only about ~5% of all blocks are visible
            int total = (int)(0.05 * Size * Size * Height);

            blocks = new Block[Height][][];
            for (int y = 0; y < Height; y++)
            {
                blocks[y] = new Block[Size][];

                for (int x = 0; x < Size; x++)
                {
                    blocks[y][x] = new Block[Size];
                }
            }

            Biomes = new BiomeType[Size][];
            for (int x = 0; x < Biomes.Length; x++)
            {
                Biomes[x] = new BiomeType[Size];
            }

            activeBlockIndexes = new HashSet<Vector3I>(total);

            //Initialize mesh
            VertexList = new List<VertexPositionTextureLight>(6 * total);
            TransparentVertexList = new List<VertexPositionTextureLight>(3 * total);

            //Initialize light
            lightQueue = new Queue<LightNode>(100);
            lightList = new List<LightNode>(100);

            chunksToUpdate = new HashSet<Chunk>(5);

            lightMap = new byte[Height][][];
            for (int y = 0; y < Height; y++)
            {
                lightMap[y] = new byte[Size][];

                for (int x = 0; x < Size; x++)
                {
                    lightMap[y][x] = new byte[Size];
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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;
using Microsoft.Xna.Framework;

using SharpCraft.Rendering;
using SharpCraft.MathUtil;
using System.Linq;


namespace SharpCraft.World
{
    public sealed partial class Chunk : IDisposable
    {
        public Vector3I Position { get; set; }
        public Vector3 Position3 { get; set; }

        public NeighborChunks Neighbors;

        public void Dispose() => Dispose(true);
        SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;

        public const int SIZE = 16;
        public const int HEIGHT = 128;
        public const int LAST = SIZE - 1;

        readonly BlockMetadataProvider blockMetadata;

        public class NeighborChunks
        {
            public Chunk ZNeg, ZPos, XNeg, XPos;
        }

        public Chunk(Vector3I position, Dictionary<Vector3I, Chunk> region, BlockMetadataProvider blockMetadata)
        {
            Position = position;
            Position3 = SIZE * new Vector3(position.X, position.Y, position.Z);

            Neighbors = new NeighborChunks();

            this.region = region;
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

            nodes = new LightNode[6];
            lightValues = new byte[6];

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

        public void Update()
        {
            if (UpdateMesh)
            {
                CalculateMesh();
            }
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

        public void GetNeighbors()
        {
            Vector3I zNegPosition = Position + new Vector3I(0, 0, -1),
                    zPosPosition = Position + new Vector3I(0, 0, 1),
                    xNegPosition = Position + new Vector3I(-1, 0, 0),
                    xPosPosition = Position + new Vector3I(1, 0, 0);

            Neighbors.ZNeg = region.TryGetValue(zNegPosition, out Chunk value) ? value : null;
            if (Neighbors.ZNeg != null && Neighbors.ZNeg.Neighbors.ZPos is null)
            {
                Neighbors.ZNeg.Neighbors.ZPos = this;
            }

            Neighbors.ZPos = region.TryGetValue(zPosPosition, out value) ? value : null;
            if (Neighbors.ZPos != null && Neighbors.ZPos.Neighbors.ZNeg is null)
            {
                Neighbors.ZPos.Neighbors.ZNeg = this;
            }

            Neighbors.XNeg = region.TryGetValue(xNegPosition, out value) ? value : null;
            if (Neighbors.XNeg != null && Neighbors.XNeg.Neighbors.XPos is null)
            {
                Neighbors.XNeg.Neighbors.XPos = this;
            }

            Neighbors.XPos = region.TryGetValue(xPosPosition, out value) ? value : null;
            if (Neighbors.XPos != null && Neighbors.XPos.Neighbors.XNeg is null)
            {
                Neighbors.XPos.Neighbors.XNeg = this;
            }
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
                Dereference();
                safeHandle?.Dispose();
            }

            disposed = true;
        }
    }
}

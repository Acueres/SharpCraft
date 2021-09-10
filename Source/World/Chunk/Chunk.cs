using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;
using Microsoft.Win32.SafeHandles;

using SharpCraft.Rendering;


namespace SharpCraft.World
{
    public sealed partial class Chunk : IDisposable
    {
        public Vector3 Position;

        public NeighborChunks Neighbors;

        public void Dispose() => Dispose(true);
        SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;

        int size;
        int height;
        int last;

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

            this.size = size;
            this.height = height;
            last = size - 1;

            this.region = region;
            this.worldGenerator = worldGenerator;

            //Only about ~5% of all blocks are visible
            int total = (int)(0.05 * size * size * height);

            blocks = new ushort?[height][][];
            for (int y = 0; y < height; y++)
            {
                blocks[y] = new ushort?[size][];

                for (int x = 0; x < size; x++)
                {
                    blocks[y][x] = new ushort?[size];
                }
            }

            BiomeData = new byte[size][];
            for (int x = 0; x < BiomeData.Length; x++)
            {
                BiomeData[x] = new byte[size];
            }

            Active = new List<Index>(total);

            //Initialize mesh
            VertexList = new List<VertexPositionTextureLight>(6 * total);
            TransparentVertexList = new List<VertexPositionTextureLight>(3 * total);

            //Initialize light
            lightQueue = new Queue<LightNode>(100);
            lightList = new List<LightNode>(100);

            nodes = new LightNode[6];
            lightValues = new byte[6];

            lightSourceValues = Assets.LightValues;

            isTransparent = Assets.TransparentBlocks;
            isLightSource = Assets.LightSources;

            chunksToUpdate = new HashSet<Chunk>(5);

            lightMap = new byte[height][][];
            for (int y = 0; y < height; y++)
            {
                lightMap[y] = new byte[size][];

                for (int x = 0; x < size; x++)
                {
                    lightMap[y][x] = new byte[size];
                }
            }

            lightSources = new List<Index>(100);

            Initialize();
            InitializeLight();
            MakeMesh();
        }

        public ushort? this[int x, int y, int z]
        {
            get => blocks[y][x][z];
            set => blocks[y][x][z] = value;
        }

        public void Update()
        {
            if (UpdateMesh)
            {
                MakeMesh();
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

        public void AddIndex(int y, int x, int z)
        {
            if (!Active.Contains(new Index(y, x, z))) 
            {
                Active.Add(new Index(y, x, z));
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

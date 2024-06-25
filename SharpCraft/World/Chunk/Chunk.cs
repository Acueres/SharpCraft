using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;
using Microsoft.Win32.SafeHandles;

using SharpCraft.Rendering;
using SharpCraft.Models;


namespace SharpCraft.World
{
    public sealed partial class Chunk : IDisposable
    {
        public Vector3 Position;

        public NeighborChunks Neighbors;

        public void Dispose() => Dispose(true);
        SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;

        public const int SIZE = 16;
        public const int HEIGHT = 128;
        public static int LAST { get; private set; }

        public class NeighborChunks
        {
            public Chunk ZNeg, ZPos, XNeg, XPos;
        }

        static Chunk()
        {
            LAST = SIZE - 1;
        }

        public Chunk(Vector3 position, WorldGenerator worldGenerator, Dictionary<Vector3, Chunk> region)
        {
            Position = position;

            Neighbors = new NeighborChunks();

            this.region = region;
            this.worldGenerator = worldGenerator;

            //Only about ~5% of all blocks are visible
            int total = (int)(0.05 * SIZE * SIZE * HEIGHT);

            blocks = new ushort?[HEIGHT][][];
            for (int y = 0; y < HEIGHT; y++)
            {
                blocks[y] = new ushort?[SIZE][];

                for (int x = 0; x < SIZE; x++)
                {
                    blocks[y][x] = new ushort?[SIZE];
                }
            }

            BiomeData = new byte[SIZE][];
            for (int x = 0; x < BiomeData.Length; x++)
            {
                BiomeData[x] = new byte[SIZE];
            }

            Active = new List<BlockIndex>(total);

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

            lightMap = new byte[HEIGHT][][];
            for (int y = 0; y < HEIGHT; y++)
            {
                lightMap[y] = new byte[SIZE][];

                for (int x = 0; x < SIZE; x++)
                {
                    lightMap[y][x] = new byte[SIZE];
                }
            }

            lightSources = new List<BlockIndex>(100);

            Initialize();
            InitializeLight();
            CalculateMesh();
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

        void GetNeighbors()
        {
            int size = 16;
            Vector3 zNegPosition = Position + new Vector3(0, 0, -size),
                    zPosPosition = Position + new Vector3(0, 0, size),
                    xNegPosition = Position + new Vector3(-size, 0, 0),
                    xPosPosition = Position + new Vector3(size, 0, 0);

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

        public void AddIndex(BlockIndex index)
        {
            if (!Active.Contains(index))
            {
                Active.Add(index);
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

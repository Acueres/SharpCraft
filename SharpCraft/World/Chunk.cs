using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;
using Microsoft.Xna.Framework;

using SharpCraft.Utility;
using SharpCraft.World.Light;

namespace SharpCraft.World
{
    public interface IChunk
    {
        Vector3I Index { get; }
        Vector3 Position { get; }
        bool IsReady { get; set; }
        bool RecalculateMesh { get; set; }

        Block this[int x, int y, int z] { get; set; }
        LightValue GetLight(int x, int y, int z);
        void SetLight(int x, int y, int z, LightValue value);
        void CalculateActiveBlocks(ChunkNeighbors neighbors);
        IEnumerable<Vector3I> GetActiveIndexes();
        FacesState GetVisibleFaces(int y, int x, int z, ChunkNeighbors neighbors, bool calculateOpacity = true);
        bool AddIndex(Vector3I index);
        bool RemoveIndex(Vector3I index);
        IEnumerable<Vector3I> GetLightSources();
        void Dispose();
        int GetHashCode();
    }

    public class SkyChunk(Vector3I index) : IChunk
    {
        public Vector3I Index { get; } = index;
        public Vector3 Position { get; } = FullChunk.Size * new Vector3(index.X, index.Y, index.Z);
        public bool IsReady { get; set; }

        public bool RecalculateMesh { get; set; }

        public Block this[int x, int y, int z]
        {
            get => Block.Empty;
            set { }
        }

        public LightValue GetLight(int x, int y, int z)
        {
            return LightValue.Sunlight;
        }

        public void SetLight(int x, int y, int z, LightValue value) { }

        public bool AddIndex(Vector3I index) => true;

        public bool RemoveIndex(Vector3I index) => true;

        public IEnumerable<Vector3I> GetActiveIndexes() => [];

        public IEnumerable<Vector3I> GetLightSources() => [];

        public void CalculateActiveBlocks(ChunkNeighbors neighbors) { }

        public FacesState GetVisibleFaces(int y, int x, int z, ChunkNeighbors neighbors,
                                    bool calculateOpacity = true) => new(false);

        public void Dispose() { }

        public override int GetHashCode()
        {
            return Index.GetHashCode();
        }
    }

    public class FullChunk : IChunk, IDisposable
    {
        public const int Size = 16;
        public const int Last = Size - 1;

        public Vector3I Index { get; }
        public Vector3 Position { get; }
        public bool IsReady { get; set; }

        readonly HashSet<Vector3I> activeBlockIndexes = [];
        readonly Block[,,] blocks;
        readonly LightValue[,,] lightMap;
        readonly HashSet<Vector3I> lightSourceIndexes = [];

        public void Dispose() => Dispose(true);
        readonly SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;

        readonly BlockMetadataProvider blockMetadata;

        public bool RecalculateMesh { get; set; }

        public static int CalculateChunkIndex(float val)
        {
            if (val < 0)
            {
                return (int)(val / Size) - 1;
            }

            return (int)(val / Size);
        }

        public FullChunk(Vector3I index, BlockMetadataProvider blockMetadata)
        {
            Index = index;
            Position = Size * new Vector3(index.X, index.Y, index.Z);

            this.blockMetadata = blockMetadata;

            blocks = new Block[Size, Size, Size];
            lightMap = new LightValue[Size, Size, Size];
        }

        public Block this[int x, int y, int z]
        {
            get => blocks[x, y, z];
            set => blocks[x, y, z] = value;
        }

        public LightValue GetLight(int x, int y, int z)
        {
            return lightMap[x, y, z];
        }

        public void SetLight(int x, int y, int z, LightValue value)
        {
            lightMap[x, y, z] = value;
        }

        public bool AddIndex(Vector3I index)
        {
            return activeBlockIndexes.Add(index);
        }

        public bool RemoveIndex(Vector3I index)
        {
            return activeBlockIndexes.Remove(index);
        }

        public void AddLightSource(int x, int y, int z)
        {
            lightSourceIndexes.Add(new Vector3I(x, y, z));
        }

        public int ActiveBlocksCount => activeBlockIndexes.Count;

        public IEnumerable<Vector3I> GetActiveIndexes()
        {
            foreach (Vector3I index in activeBlockIndexes) yield return index;
        }

        public IEnumerable<Vector3I> GetLightSources()
        {
            foreach (Vector3I index in lightSourceIndexes) yield return index;
        }

        public override int GetHashCode()
        {
            return Index.GetHashCode();
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

        public void CalculateActiveBlocks(ChunkNeighbors neighbors)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    for (int z = 0; z < Size; z++)
                    {
                        if (blocks[x, y, z].IsEmpty)
                        {
                            continue;
                        }

                        FacesState visibleFaces = GetVisibleFaces(y, x, z, neighbors, calculateOpacity: false);

                        if (visibleFaces.Any())
                        {
                            AddIndex(new(x, y, z));
                        }
                    }
                }
            }
        }

        public FacesState GetVisibleFaces(int y, int x, int z, ChunkNeighbors neighbors,
                                    bool calculateOpacity = true)
        {
            FacesState visibleFaces = new();

            Block block = blocks[x, y, z];

            Block adjacentBlock;

            bool blockOpaque = true;
            if (calculateOpacity)
            {
                blockOpaque = !(block.IsEmpty || blockMetadata.IsBlockTransparent(block.Value));
            }

            if (z == Last)
            {
                adjacentBlock = neighbors.ZPos[x, y, 0];
            }
            else
            {
                adjacentBlock = blocks[x, y, z + 1];
            }
            visibleFaces.ZPos = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (z == 0)
            {
                adjacentBlock = neighbors.ZNeg[x, y, Last];
            }
            else
            {
                adjacentBlock = blocks[x, y, z - 1];
            }
            visibleFaces.ZNeg = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (y == Last)
            {
                adjacentBlock = neighbors.YPos[x, 0, z];
            }
            else
            {
                adjacentBlock = blocks[x, y + 1, z];
            }
            visibleFaces.YPos = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (y == 0)
            {
                adjacentBlock = neighbors.YNeg[x, Last, z];
            }
            else
            {
                adjacentBlock = blocks[x, y - 1, z];
            }
            visibleFaces.YNeg = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);


            if (x == Last)
            {
                adjacentBlock = neighbors.XPos[0, y, z];
            }
            else
            {
                adjacentBlock = blocks[x + 1, y, z];
            }
            visibleFaces.XPos = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (x == 0)
            {
                adjacentBlock = neighbors.XNeg[Last, y, z];
            }
            else
            {
                adjacentBlock = blocks[x - 1, y, z];
            }
            visibleFaces.XNeg = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            return visibleFaces;
        }
    }
}

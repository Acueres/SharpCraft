﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;
using Microsoft.Xna.Framework;

using System.Linq;
using SharpCraft.Utility;

namespace SharpCraft.World
{
    public class Chunk : IDisposable
    {
        public Vector3I Index { get; }
        public Vector3 Position { get; }
        public bool IsReady { get; set; }

        readonly HashSet<Vector3I> activeBlockIndexes = [];

        readonly Block[,,] blocks;

        public void Dispose() => Dispose(true);
        readonly SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;

        public const int Size = 16;
        public const int Last = Size - 1;

        readonly BlockMetadataProvider blockMetadata;

        public bool RecalculateMesh { get; set; }

        readonly LightValue[,,] lightMap;
        readonly HashSet<Vector3I> lightSourceIndexes = [];

        public static int CalculateChunkIndex(float val)
        {
            if (val < 0)
            {
                return (int)(val / Size) - 1;
            }

            return (int)(val / Size);
        }

        public Chunk(Vector3I position, BlockMetadataProvider blockMetadata)
        {
            Index = position;
            Position = Size * new Vector3(position.X, position.Y, position.Z);

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

        public Vector3I GetIndex(int i)
        {
            return activeBlockIndexes.Where((index, id) => id == i).Single();
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

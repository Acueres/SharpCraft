using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;
using Microsoft.Xna.Framework;
using SharpCraft.MathUtilities;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Light;

namespace SharpCraft.World.Chunks;

public class Chunk : IDisposable
{
    public const int Size = 16;
    public const int Last = Size - 1;

    public Vector3I Index { get; }
    public Vector3 Position { get; }
    public bool IsReady { get; set; }

    public bool IsEmpty => blocks is null;

    Block[,,] blocks;
    LightValue[,,] lightMap;

    readonly HashSet<Vector3I> activeBlockIndexes = [];
    readonly HashSet<Vector3I> lightSourceIndexes = [];

    public void Dispose() => Dispose(true);
    readonly SafeHandle safeHandle = new SafeFileHandle(nint.Zero, true);
    bool disposed = false;

    readonly BlockMetadataProvider blockMetadata;

    public bool RecalculateMesh { get; set; }

    public Chunk(Vector3I index)
    {
        Index = index;
        Position = Size * new Vector3(index.X, index.Y, index.Z);
    }

    public Chunk(Vector3I index, BlockMetadataProvider blockMetadata)
    {
        Index = index;
        Position = Size * new Vector3(index.X, index.Y, index.Z);

        this.blockMetadata = blockMetadata;
    }

    public void Init()
    {
        blocks = new Block[Size, Size, Size];
        lightMap = new LightValue[Size, Size, Size];
    }

    public Block this[int x, int y, int z]
    {
        get => blocks is null ? Block.Empty : blocks[x, y, z];
        set
        {
            if (blocks is not null)
            {
                blocks[x, y, z] = value;
            }
        }
    }

    public LightValue GetLight(int x, int y, int z)
    {
        return lightMap is null ? LightValue.Sunlight : lightMap[x, y, z];
    }

    public void SetLight(int x, int y, int z, LightValue value)
    {
        if (lightMap is not null)
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

    public void ActivateBlock(Vector3I index)
    {
        if (!blocks[index.X, index.Y, index.Z].IsEmpty)
        {
            AddIndex(index);
            RecalculateMesh = true;
        }
    }

    public void CalculateActiveBlocks(ChunkAdjacency adjacency)
    {
        if (IsEmpty) return;

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

                    FacesState visibleFaces = GetVisibleFaces(new Vector3I(x, y, z), adjacency, calculateOpacity: false);

                    if (visibleFaces.Any())
                    {
                        AddIndex(new(x, y, z));
                    }
                }
            }
        }
    }

    public FacesState GetVisibleFaces(Vector3I index, ChunkAdjacency adjacency,
                                bool calculateOpacity = true)
    {
        FacesState visibleFaces = new();

        int x = index.X;
        int y = index.Y;
        int z = index.Z;

        Block block = blocks[x, y, z];

        Block adjacentBlock;

        bool blockOpaque = true;
        if (calculateOpacity)
        {
            blockOpaque = !(block.IsEmpty || blockMetadata.IsBlockTransparent(block.Value));
        }

        if (z == Last)
        {
            adjacentBlock = adjacency.ZPos.Root[x, y, 0];
        }
        else
        {
            adjacentBlock = blocks[x, y, z + 1];
        }
        visibleFaces.ZPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque;

        if (z == 0)
        {
            adjacentBlock = adjacency.ZNeg.Root[x, y, Last];
        }
        else
        {
            adjacentBlock = blocks[x, y, z - 1];
        }
        visibleFaces.ZNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque;

        if (y == Last)
        {
            adjacentBlock = adjacency.YPos.Root[x, 0, z];
        }
        else
        {
            adjacentBlock = blocks[x, y + 1, z];
        }
        visibleFaces.YPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque;

        if (y == 0)
        {
            adjacentBlock = adjacency.YNeg.Root[x, Last, z];
        }
        else
        {
            adjacentBlock = blocks[x, y - 1, z];
        }
        visibleFaces.YNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque;


        if (x == Last)
        {
            adjacentBlock = adjacency.XPos.Root[0, y, z];
        }
        else
        {
            adjacentBlock = blocks[x + 1, y, z];
        }
        visibleFaces.XPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque;

        if (x == 0)
        {
            adjacentBlock = adjacency.XNeg.Root[Last, y, z];
        }
        else
        {
            adjacentBlock = blocks[x - 1, y, z];
        }
        visibleFaces.XNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque;

        return visibleFaces;
    }

    public static int WorldToChunkIndex(float worldCoord)
    {
        if (worldCoord < 0)
        {
            return (int)(worldCoord / Size) - 1;
        }

        return (int)(worldCoord / Size);
    }

    public static Vector3I WorldToChunkCoords(Vector3 pos)
    {
        return new Vector3I(WorldToChunkIndex(pos.X), WorldToChunkIndex(pos.Y), WorldToChunkIndex(pos.Z));
    }

    public static Vector3I WorldToBlockCoords(Vector3 pos)
    {
        return new Vector3I(WorldToBlockIndex(pos.X), WorldToBlockIndex(pos.Y), WorldToBlockIndex(pos.Z));
    }

    static int WorldToBlockIndex(float worldCoord)
    {
        int index = (int)Math.Floor(worldCoord);
        return ((index % Size) + Size) % Size;
    }

    public static Vector3 BlockIndexToWorldPosition(Vector3 chunkPosition, Vector3I blockIndex)
    {
        return new Vector3(blockIndex.X, blockIndex.Y, blockIndex.Z) + chunkPosition;
    }
}

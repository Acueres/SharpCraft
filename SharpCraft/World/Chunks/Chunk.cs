using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;
using Microsoft.Xna.Framework;

using SharpCraft.MathUtilities;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Lighting;

namespace SharpCraft.World.Chunks;

public class Chunk(Vector3I index, BlockMetadataProvider blockMetadata) : IDisposable
{
    public const int Size = 16;
    public const int Last = Size - 1;

    public Vector3I Index { get; } = index;
    public Vector3 Position { get; } = Size * new Vector3(index.X, index.Y, index.Z);
    public bool IsReady { get; set; }

    public bool IsEmpty => blocks is null;

    Block[,,] blocks;
    LightValue[,,] lightMap;

    readonly HashSet<Vector3I> surfaceIndexes = [];
    readonly HashSet<Vector3I> transparentIndexes = [];
    readonly HashSet<(Vector3I, Block)> lightSources = [];

    public void Dispose() => Dispose(true);
    readonly SafeHandle safeHandle = new SafeFileHandle(nint.Zero, true);
    bool disposed = false;

    readonly BlockMetadataProvider blockMetadata = blockMetadata;

    public bool RecalculateMesh { get; set; }

    public void SetBlockData(Block[,,] blocks)
    {
        this.blocks = blocks;
    }

    public void Init()
    {
        blocks = new Block[Size, Size, Size];
        lightMap = new LightValue[Size, Size, Size];
    }

    public void InitLight()
    {
        lightMap = new LightValue[Size, Size, Size];
    }

    public static Block[,,] GetBlockArray()
    {
        return new Block[Size, Size, Size];
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

    public void AddLightSource(int x, int y, int z, Block block)
    {
        lightSources.Add((new Vector3I(x, y, z), block));
    }

    public IEnumerable<Vector3I> GetActiveBlocksIndexes()
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

                    yield return new Vector3I(x, y, z);
                }
            }
        }
    }

    public IEnumerable<(Vector3I, Block)> GetLightSources()
    {
        foreach (var data in lightSources) yield return data;
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

    public void GenerateIndexCaches(ChunkBuffer buffer, ChunkAdjacency adjacency)
    {
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                for (int z = 0; z < Size; z++)
                {
                    Block block = buffer[x, y, z];
                    if (block.IsEmpty)
                    {
                        continue;
                    }

                    var index = new Vector3I(x, y, z);
                    var visibleFaces = GetVisibleFaces(index, adjacency);

                    if (!visibleFaces.Any()) continue;

                    surfaceIndexes.Add(index);

                    if (blockMetadata.IsBlockTransparent(block))
                    {
                        transparentIndexes.Add(index);
                    }
                }
            }
        }
    }

    public void GenerateIndexCaches(ChunkAdjacency adjacency)
    {
        surfaceIndexes.Clear();
        transparentIndexes.Clear();

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                for (int z = 0; z < Size; z++)
                {
                    Block block = blocks[x, y, z];
                    if (block.IsEmpty)
                    {
                        continue;
                    }

                    var index = new Vector3I(x, y, z);
                    var visibleFaces = GetVisibleFaces(index, adjacency);

                    if (!visibleFaces.Any()) continue;

                    surfaceIndexes.Add(index);

                    if (blockMetadata.IsBlockTransparent(block))
                    {
                        transparentIndexes.Add(index);
                    }
                }
            }
        }
    }

    public IEnumerable<Vector3I> GetVisibleBlocks()
    {
        foreach (var index in surfaceIndexes) yield return index;
    }

    public bool IsBlockTransparent(Vector3I index)
    {
        return transparentIndexes.Contains(index) || !surfaceIndexes.Contains(index);
    }

    public bool IsBlockTransparentSolid(Vector3I index)
    {
        return transparentIndexes.Contains(index);
    }

    public FacesState GetVisibleFaces(Vector3I index, ChunkAdjacency adjacency)
    {
        FacesState visibleFaces = new();

        int x = index.X;
        int y = index.Y;
        int z = index.Z;

        Block block = blocks[x, y, z];

        Block adjacentBlock;

        bool isBlockOpaque = !(block.IsEmpty || blockMetadata.IsBlockTransparent(block));

        if (z == Last)
        {
            adjacentBlock = adjacency.ZPos.Root[x, y, 0];
        }
        else
        {
            adjacentBlock = blocks[x, y, z + 1];
        }
        visibleFaces.ZPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (z == 0)
        {
            adjacentBlock = adjacency.ZNeg.Root[x, y, Last];
        }
        else
        {
            adjacentBlock = blocks[x, y, z - 1];
        }
        visibleFaces.ZNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (y == Last)
        {
            adjacentBlock = adjacency.YPos.Root[x, 0, z];
        }
        else
        {
            adjacentBlock = blocks[x, y + 1, z];
        }
        visibleFaces.YPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (y == 0)
        {
            adjacentBlock = adjacency.YNeg.Root[x, Last, z];
        }
        else
        {
            adjacentBlock = blocks[x, y - 1, z];
        }
        visibleFaces.YNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;


        if (x == Last)
        {
            adjacentBlock = adjacency.XPos.Root[0, y, z];
        }
        else
        {
            adjacentBlock = blocks[x + 1, y, z];
        }
        visibleFaces.XPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (x == 0)
        {
            adjacentBlock = adjacency.XNeg.Root[Last, y, z];
        }
        else
        {
            adjacentBlock = blocks[x - 1, y, z];
        }
        visibleFaces.XNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

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

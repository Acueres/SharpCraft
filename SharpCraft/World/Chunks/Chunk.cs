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

public class Chunk : IDisposable
{
    public const byte Size = 16;
    public const byte Last = Size - 1;

    public Vector3I Index { get; }
    public Vector3 Position { get; }
    public bool IsReady { get; set; }

    public bool IsEmpty { get; internal set; }

    List<Block> palette;
    Dictionary<Block, uint> paletteIndexMap;
    BitStorage storage;
    LightValue[,,] lightMap;

    readonly HashSet<Vector3I> surfaceIndexes = [];
    readonly HashSet<Vector3I> transparentIndexes = [];
    readonly HashSet<(Vector3I, Block)> lightSources = [];

    public void Dispose() => Dispose(true);
    readonly SafeHandle safeHandle = new SafeFileHandle(nint.Zero, true);
    bool disposed = false;

    readonly BlockMetadataProvider blockMetadata;

    public bool RecalculateMesh { get; set; }

    public Chunk(Vector3I index, BlockMetadataProvider blockMetadata)
    {
        Index = index;
        Position = Size * new Vector3(index.X, index.Y, index.Z);
        this.blockMetadata = blockMetadata;
    }

    public void Init(Block[,,] buffer)
    {
        if (buffer == null)
        {
            IsEmpty = true;
            return;
        }

        var uniqueBlocks = GetUniqueBlocks(buffer);
        if (uniqueBlocks.Count == 1 && uniqueBlocks.Contains(Block.Empty))
        {
            IsEmpty = true;
            return;
        }

        uint blockIndex = 0;
        palette = new(uniqueBlocks.Count);
        paletteIndexMap = [];
        foreach (var block in uniqueBlocks)
        {
            palette.Add(block);
            paletteIndexMap.Add(block, blockIndex);
            blockIndex++;
        }

        int bitsPerBlock = GetBitsPerBlock(palette.Count);
        storage = new BitStorage(Size, bitsPerBlock);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                for (int z = 0; z < Size; z++)
                {
                    storage[x, y, z] = paletteIndexMap[buffer[x, y, z]];
                }
            }
        }
    }

    static HashSet<Block> GetUniqueBlocks(Block[,,] buffer)
    {
        // Extract unique block types
        HashSet<Block> uniqueBlocks = [];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                for (int z = 0; z < Size; z++)
                {
                    uniqueBlocks.Add(buffer[x, y, z]);
                }
            }
        }

        return uniqueBlocks;
    }

    static int GetBitsPerBlock(int count)
    {
        if (count <= 1) return 1;
        return (int)Math.Log2(count - 1) + 1;
    }

    public void Init()
    {
        if (IsEmpty)
        {
            palette = [Block.Empty];
            paletteIndexMap = [];
            paletteIndexMap.Add(Block.Empty, 0);
            IsEmpty = false;
        }
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
        get
        {
            if (IsEmpty) return Block.Empty;
            uint id = storage[x, y, z];
            return palette[(int)id];
        }
        set
        {
            if (!paletteIndexMap.TryGetValue(value, out uint index))
            {
                index = (uint)paletteIndexMap.Count;
                palette.Add(value);
                paletteIndexMap.Add(value, index);

                // Check if the palette size has reached the next power of two, then resize
                if ((palette.Count & (palette.Count - 1)) == 0)
                {
                    ResizeStorage();
                }
            }

            storage[x, y, z] = index;
        }
    }

    void ResizeStorage()
    {
        int bitsPerBlock = GetBitsPerBlock(palette.Count);
        var resizedStorage = new BitStorage(Size, bitsPerBlock);
        if (storage == null)
        {
            storage = resizedStorage;
            return;
        }

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                for (int z = 0; z < Size; z++)
                {

                    resizedStorage[x, y, z] = storage[x, y, z];
                }
            }
        }

        storage = resizedStorage;
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
                    if (this[x, y, z].IsEmpty)
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
                    Block block = this[x, y, z];
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

        Block block = this[x, y, z];

        Block adjacentBlock;

        bool isBlockOpaque = !(block.IsEmpty || blockMetadata.IsBlockTransparent(block));

        if (z == Last)
        {
            adjacentBlock = adjacency.ZPos.Root[x, y, 0];
        }
        else
        {
            adjacentBlock = this[x, y, z + 1];
        }
        visibleFaces.ZPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (z == 0)
        {
            adjacentBlock = adjacency.ZNeg.Root[x, y, Last];
        }
        else
        {
            adjacentBlock = this[x, y, z - 1];
        }
        visibleFaces.ZNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (y == Last)
        {
            adjacentBlock = adjacency.YPos.Root[x, 0, z];
        }
        else
        {
            adjacentBlock = this[x, y + 1, z];
        }
        visibleFaces.YPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (y == 0)
        {
            adjacentBlock = adjacency.YNeg.Root[x, Last, z];
        }
        else
        {
            adjacentBlock = this[x, y - 1, z];
        }
        visibleFaces.YNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;


        if (x == Last)
        {
            adjacentBlock = adjacency.XPos.Root[0, y, z];
        }
        else
        {
            adjacentBlock = this[x + 1, y, z];
        }
        visibleFaces.XPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (x == 0)
        {
            adjacentBlock = adjacency.XNeg.Root[Last, y, z];
        }
        else
        {
            adjacentBlock = this[x - 1, y, z];
        }
        visibleFaces.XNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        return visibleFaces;
    }

    public FacesState GetVisibleFaces(Vector3I index, ChunkBuffer buffer, ChunkAdjacency adjacency)
    {
        FacesState visibleFaces = new();

        int x = index.X;
        int y = index.Y;
        int z = index.Z;

        Block block = buffer[x, y, z];

        Block adjacentBlock;

        bool isBlockOpaque = !(block.IsEmpty || blockMetadata.IsBlockTransparent(block));

        if (z == Last)
        {
            adjacentBlock = adjacency.ZPos.Root[x, y, 0];
        }
        else
        {
            adjacentBlock = buffer[x, y, z + 1];
        }
        visibleFaces.ZPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (z == 0)
        {
            adjacentBlock = adjacency.ZNeg.Root[x, y, Last];
        }
        else
        {
            adjacentBlock = buffer[x, y, z - 1];
        }
        visibleFaces.ZNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (y == Last)
        {
            adjacentBlock = adjacency.YPos.Root[x, 0, z];
        }
        else
        {
            adjacentBlock = buffer[x, y + 1, z];
        }
        visibleFaces.YPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (y == 0)
        {
            adjacentBlock = adjacency.YNeg.Root[x, Last, z];
        }
        else
        {
            adjacentBlock = buffer[x, y - 1, z];
        }
        visibleFaces.YNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;


        if (x == Last)
        {
            adjacentBlock = adjacency.XPos.Root[0, y, z];
        }
        else
        {
            adjacentBlock = buffer[x + 1, y, z];
        }
        visibleFaces.XPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (x == 0)
        {
            adjacentBlock = adjacency.XNeg.Root[Last, y, z];
        }
        else
        {
            adjacentBlock = buffer[x - 1, y, z];
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

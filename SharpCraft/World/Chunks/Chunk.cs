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

public enum ChunkState
{
    Allocated,     // Chunk created
    Generated,     // Blocks complete
    Linked,        // Linking complete
    LightSeeded,   // Skylight & block light sources queued
    Lit,           // BFS finished
    Ready,         // Uploaded to GPU / visible
    Unloaded       // Removed from region & caches
}

public class Chunk(Vec3<int> index, BlockMetadataProvider blockMetadata) : IDisposable
{
    public const byte Size = 16;
    public const byte Last = Size - 1;

    public Vec3<int> Index { get; } = index;
    public Vector3 Position { get; } = Size * new Vector3(index.X, index.Y, index.Z);

    public ChunkState State { get; set; }
    public bool IsEmpty => palette is null;
    public bool IsReady => State == ChunkState.Ready;

    //Adjacent chunk references
    public Chunk XNeg { get; set; }
    public Chunk XPos { get; set; }
    public Chunk YNeg { get; set; }
    public Chunk YPos { get; set; }
    public Chunk ZNeg { get; set; }
    public Chunk ZPos { get; set; }

    public bool AllNeighborsExist => XNeg != null && XPos != null && YNeg != null && YPos != null && ZNeg != null && ZPos != null;

    List<Block> palette;
    Dictionary<Block, uint> paletteIndexMap;
    BitStorage storage;
    LightValue[,,] lightMap;

    readonly HashSet<(Vec3<byte>, Block)> lightSources = [];

    public void Dispose() => Dispose(true);
    readonly SafeHandle safeHandle = new SafeFileHandle(nint.Zero, true);
    bool disposed = false;

    readonly BlockMetadataProvider blockMetadata = blockMetadata;

    public void BuildPalette(Block[,,] buffer)
    {
        if (buffer is null)
        {
            return;
        }

        var uniqueBlocks = GetUniqueBlocks(buffer);
        if (uniqueBlocks.Count == 1 && uniqueBlocks.Contains(Block.Empty))
        {
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
            State = ChunkState.Generated;
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

    public IEnumerable<Chunk> GetNeighbours()
    {
        if (XNeg != null) yield return XNeg;
        if (XPos != null) yield return XPos;
        if (YNeg != null) yield return YNeg;
        if (YPos != null) yield return YPos;
        if (ZNeg != null) yield return ZNeg;
        if (ZPos != null) yield return ZPos;
    }

    public int? GetMaximumTerrainElevation()
    {
        if (IsEmpty)
        {
            return null;
        }

        int? maxElevation = null;

        for (int x = 0; x < Size; x++)
        {
            for (int z = 0; z < Size; z++)
            {
                // Scan vertically downwards for the current (X, Z) column
                for (int y = Last; y >= 0; y--)
                {
                    Block currentBlock = this[x, y, z];

                    if (!currentBlock.IsEmpty)
                    {
                        // Found the highest non-empty block in this column.
                        if (!maxElevation.HasValue || y > maxElevation.Value)
                        {
                            maxElevation = y;
                        }

                        break;
                    }
                }

                /* If we have found a block at the absolute maximum height possible for the chunk,
                 no other column can possibly have a higher block, so we can stop searching entirely.*/
                if (maxElevation.HasValue && maxElevation.Value == Last)
                {
                    maxElevation = maxElevation.Value + Index.Y * Size;
                    return maxElevation;
                }
            }
        }

        if (maxElevation.HasValue)
        {
            maxElevation = maxElevation.Value + Index.Y * Size;
        }

        return maxElevation;
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

    public void AddLightSource(byte x, byte y, byte z, Block block)
    {
        lightSources.Add((new Vec3<byte>(x, y, z), block));
    }

    public IEnumerable<(Vec3<byte>, Block)> GetLightSources()
    {
        foreach (var data in lightSources) yield return data;
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

    public Chunk GetNeighborFromOffset(Vec3<sbyte> offset)
    {
        //Assuming all other components are zero
        if (offset.X == 1)
            return XPos;
        if (offset.X == -1)
            return XNeg;
        if (offset.Y == 1)
            return YPos;
        if (offset.Y == -1)
            return YNeg;
        if (offset.Z == 1)
            return ZPos;
        if (offset.Z == -1)
            return ZNeg;

        return this;
    }

    public IEnumerable<Vec3<byte>> GetVisibleBlocks()
    {
        for (byte y = 0; y < Size; y++)
        {
            for (byte x = 0; x < Size; x++)
            {
                for (byte z = 0; z < Size; z++)
                {
                    Block block = this[x, y, z];
                    if (block.IsEmpty)
                    {
                        continue;
                    }

                    var index = new Vec3<byte>(x, y, z);
                    var visibleFaces = GetVisibleFaces(index);

                    if (!visibleFaces.Any()) continue;

                    yield return index;
                }
            }
        }
    }

    public bool IsBlockTransparent(Vec3<int> index)
    {
        Block block = this[index.X, index.Y, index.Z];
        return block.IsEmpty || blockMetadata.IsBlockTransparent(block);
    }

    public bool IsBlockTransparentSolid(Vec3<int> index)
    {
        Block block = this[index.X, index.Y, index.Z];
        return blockMetadata.IsBlockTransparent(block);
    }

    public bool IsBlockLightSource(Vec3<int> index)
    {
        Block block = this[index.X, index.Y, index.Z];
        return blockMetadata.IsLightSource(block);
    }

    public byte GetLightSourceValue(Vec3<int> index)
    {
        Block block = this[index.X, index.Y, index.Z];
        return blockMetadata.GetLightSourceValue(block);
    }

    public FacesState GetVisibleFaces(Vec3<byte> index)
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
            adjacentBlock = ZPos[x, y, 0];
        }
        else
        {
            adjacentBlock = this[x, y, z + 1];
        }
        visibleFaces.ZPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (z == 0)
        {
            adjacentBlock = ZNeg[x, y, Last];
        }
        else
        {
            adjacentBlock = this[x, y, z - 1];
        }
        visibleFaces.ZNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (y == Last)
        {
            adjacentBlock = YPos[x, 0, z];
        }
        else
        {
            adjacentBlock = this[x, y + 1, z];
        }
        visibleFaces.YPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (y == 0)
        {
            adjacentBlock = YNeg[x, Last, z];
        }
        else
        {
            adjacentBlock = this[x, y - 1, z];
        }
        visibleFaces.YNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;


        if (x == Last)
        {
            adjacentBlock = XPos[0, y, z];
        }
        else
        {
            adjacentBlock = this[x + 1, y, z];
        }
        visibleFaces.XPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock) && isBlockOpaque;

        if (x == 0)
        {
            adjacentBlock = XNeg[Last, y, z];
        }
        else
        {
            adjacentBlock = this[x - 1, y, z];
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

    public static Vec3<int> WorldToChunkCoords(Vector3 pos)
    {
        return new Vec3<int>(WorldToChunkIndex(pos.X), WorldToChunkIndex(pos.Y), WorldToChunkIndex(pos.Z));
    }

    public static Vec3<byte> WorldToBlockCoords(Vector3 pos)
    {
        return new Vec3<byte>(WorldToBlockIndex(pos.X), WorldToBlockIndex(pos.Y), WorldToBlockIndex(pos.Z));
    }

    static byte WorldToBlockIndex(float worldCoord)
    {
        int index = (int)Math.Floor(worldCoord);
        return (byte)(((index % Size) + Size) % Size);
    }

    public static Vector3 BlockIndexToWorldPosition(Vector3 chunkPosition, Vec3<byte> blockIndex)
    {
        return new Vector3(blockIndex.X, blockIndex.Y, blockIndex.Z) + chunkPosition;
    }

    public override bool Equals(object obj)
    {
        if (obj is not Chunk other) return false;
        return this == other;
    }

    public override int GetHashCode()
    {
        return Index.GetHashCode();
    }
}

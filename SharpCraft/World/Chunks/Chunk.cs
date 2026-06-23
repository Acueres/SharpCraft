using SharpCraft.SharpMath;
using SharpCraft.World.Blocks;
using SharpCraft.World.Lighting;

using System.Numerics;
using System.Collections.Concurrent;

namespace SharpCraft.World.Chunks;

internal class Chunk(Vec3<int> index, BlockRegistry blockRegistry)
{
    public const byte Size = 16;
    public const float HalfSize = Size * 0.5f;
    public const byte Last = Size - 1;

    public Vec3<int> Index { get; } = index;
    public Vector3 Position { get; } = Size * new Vector3(index.X, index.Y, index.Z);

    public bool IsEmpty => palette is null;
    public int PaletteCount => palette?.Count ?? 0;
    public bool IsReady { get; set; }

    public readonly object SyncRoot = new();

    public NeighborSet Neighbors =>
        new()
        {
            ZPos = ZPos,
            ZNeg = ZNeg,
            XPos = XPos,
            XNeg = XNeg,
            YPos = YPos,
            YNeg = YNeg
        };

    //Adjacent chunk references
    public Chunk? ZPos { get; set; }
    public Chunk? ZNeg { get; set; }
    public Chunk? XPos { get; set; }
    public Chunk? XNeg { get; set; }
    public Chunk? YPos { get; set; }
    public Chunk? YNeg { get; set; }

    private List<Block>? palette;
    private Dictionary<Block, uint>? paletteIndexMap;
    private BitStorage? storage;
    private LightValue[,,] lightMap;

    public ConcurrentQueue<(LightValue Value, byte X, byte Y, byte Z)> LightQueue { get; } = [];
    private readonly HashSet<Vec3<byte>> lightSources = [];

    public void EnqueueLight(in LightNode lightNode)
    {
        LightQueue.Enqueue((lightNode.Value, (byte)lightNode.X, (byte)lightNode.Y, (byte)lightNode.Z));
    }
    
    [ThreadStatic]
    private static Block[,,]? buffer;
    
    public void BuildPalette(Block[,,]? b)
    {
        if (b is null)
        {
            return;
        }

        var uniqueBlocks = GetUniqueBlocks(b);
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
                    storage[x, y, z] = paletteIndexMap[b[x, y, z]];
                }
            }
        }
    }
    
    public void RebuildPalette()
    {
        if (storage is null)
        {
            palette = null;
            paletteIndexMap = null;
            lightSources.Clear();
            return;
        }
        
        Block[,,] buffer = GetBuffer();
        bool anyNonEmpty = false;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        for (int z = 0; z < Size; z++)
        {
            Block b = this[x, y, z];
            buffer[x, y, z] = b;
            if (!b.IsEmpty) anyNonEmpty = true;
        }

        if (!anyNonEmpty)
        {
            palette = null;
            paletteIndexMap = null;
            storage = null;
            lightSources.Clear();
            return;
        }
        
        BuildPalette(buffer);
    }

    private static HashSet<Block> GetUniqueBlocks(Block[,,] buffer)
    {
        // Extract unique block types
        HashSet<Block> uniqueBlocks = [];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        for (int z = 0; z < Size; z++)
        {
            uniqueBlocks.Add(buffer[x, y, z]);
        }

        return uniqueBlocks;
    }

    private static int GetBitsPerBlock(int count)
    {
        if (count <= 1) return 1;
        return BitOperations.Log2((uint)(count - 1)) + 1;
    }

    public void Init()
    {
        if (IsEmpty)
        {
            palette = [Block.Empty];
            paletteIndexMap = [];
            paletteIndexMap.Add(Block.Empty, 0);
        }
        lightMap = new LightValue[Size, Size, Size];
    }

    public void EnsureLight()
    {
        lightMap ??= new LightValue[Size, Size, Size];
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

                // Check if the palette size has exceeded a power of two, then resize
                // Here index is the old palette size. If it was 4, and we added a new block, we must increase the storage
                if ((index & (index - 1)) == 0)
                {
                    ResizeStorage();
                }
            }

            storage[x, y, z] = index;
        }
    }

    public uint GetStorageValue(int x, int y, int z)
    {
        uint id = storage[x, y, z];
        return id;
    }

    public uint GetPaletteValue(int p) => palette[p].Value;

    public IEnumerable<Vec3<int>> GetNeighborIndexes()
    {
        yield return Index + new Vec3<int>(-1, 0, 0);
        yield return Index + new Vec3<int>(1, 0, 0);
        yield return Index + new Vec3<int>(0, -1, 0);
        yield return Index + new Vec3<int>(0, 1, 0);
        yield return Index + new Vec3<int>(0, 0, -1);
        yield return Index + new Vec3<int>(0, 0, 1);
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

    private void ResizeStorage()
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
        EnsureLight();
        return lightMap[x, y, z];
    }

    public void SetLight(int x, int y, int z, LightValue value)
    {
        EnsureLight();
        lightMap[x, y, z] = value;
    }

    public void AddLightSource(byte x, byte y, byte z, Block block)
    {
        lightSources.Add(new Vec3<byte>(x, y, z));
    }

    public void RemoveLightSource(byte x, byte y, byte z, Block block)
    {
        lightSources.Remove(new Vec3<byte>(x, y, z));
    }

    public byte GetLightSourceValue(Vec3<int> index)
    {
        Block block = this[index.X, index.Y, index.Z];
        return blockRegistry.GetLightLevel(block);
    }

    public IEnumerable<Vec3<byte>> GetLightSources()
    {
        foreach (var sourceIndex in lightSources) yield return sourceIndex;
    }

    /*public Chunk GetNeighborFromOffset(Vec3<sbyte> offset)
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
    }*/

    public IEnumerable<(Vec3<byte> Index, FacesState Faces)> GetVisibleBlocks(NeighborSet neighbors)
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
                    var visibleFaces = GetVisibleFaces(index, neighbors);

                    if (!visibleFaces.Any()) continue;

                    yield return (index, visibleFaces);
                }
            }
        }
    }

    public bool IsBlockTransparent(int x, int y, int z)
    {
        Block block = this[x, y, z];
        return block.IsEmpty || blockRegistry.IsTransparent(block);
    }

    public FacesState GetVisibleFaces(Vec3<byte> index, in NeighborSet neighbors)
    {
        FacesState visibleFaces = new();

        int x = index.X;
        int y = index.Y;
        int z = index.Z;

        Block block = this[x, y, z];

        Block adjacentBlock;

        bool isBlockOpaque = !(block.IsEmpty || blockRegistry.IsTransparent(block));

        if (z == Last)
        {
            adjacentBlock = neighbors.ZPos![x, y, 0];
        }
        else
        {
            adjacentBlock = this[x, y, z + 1];
        }
        visibleFaces.ZPos = isBlockOpaque && (adjacentBlock.IsEmpty || blockRegistry.IsTransparent(adjacentBlock));

        if (z == 0)
        {
            adjacentBlock = neighbors.ZNeg![x, y, Last];
        }
        else
        {
            adjacentBlock = this[x, y, z - 1];
        }
        visibleFaces.ZNeg = isBlockOpaque && (adjacentBlock.IsEmpty || blockRegistry.IsTransparent(adjacentBlock));

        if (y == Last)
        {
            adjacentBlock = neighbors.YPos![x, 0, z];
        }
        else
        {
            adjacentBlock = this[x, y + 1, z];
        }
        visibleFaces.YPos = isBlockOpaque && (adjacentBlock.IsEmpty || blockRegistry.IsTransparent(adjacentBlock));

        if (y == 0)
        {
            adjacentBlock = neighbors.YNeg![x, Last, z];
        }
        else
        {
            adjacentBlock = this[x, y - 1, z];
        }
        visibleFaces.YNeg = isBlockOpaque && (adjacentBlock.IsEmpty || blockRegistry.IsTransparent(adjacentBlock));


        if (x == Last)
        {
            adjacentBlock = neighbors.XPos![0, y, z];
        }
        else
        {
            adjacentBlock = this[x + 1, y, z];
        }
        visibleFaces.XPos = isBlockOpaque && (adjacentBlock.IsEmpty || blockRegistry.IsTransparent(adjacentBlock));

        if (x == 0)
        {
            adjacentBlock = neighbors.XNeg![Last, y, z];
        }
        else
        {
            adjacentBlock = this[x - 1, y, z];
        }
        visibleFaces.XNeg = isBlockOpaque && (adjacentBlock.IsEmpty || blockRegistry.IsTransparent(adjacentBlock));

        return visibleFaces;
    }
    
    public static Block[,,] GetBuffer()
    {
        var b = buffer ??= new Block[Size, Size, Size];
        Array.Clear(b);
        return b;
    }

    public static int WorldToChunkIndex(float worldCoord)
    {
        return (int)Math.Floor(worldCoord / Size);
    }

    public static Vec3<int> WorldToChunkCoords(Vector3 pos)
    {
        return new Vec3<int>(WorldToChunkIndex(pos.X), WorldToChunkIndex(pos.Y), WorldToChunkIndex(pos.Z));
    }

    public static Vec3<byte> WorldToBlockCoords(Vector3 pos)
    {
        return new Vec3<byte>(WorldToBlockIndex(pos.X), WorldToBlockIndex(pos.Y), WorldToBlockIndex(pos.Z));
    }

    private static byte WorldToBlockIndex(float worldCoord)
    {
        int index = (int)Math.Floor(worldCoord);
        return (byte)(((index % Size) + Size) % Size);
    }

    public static Vector3 BlockIndexToWorldPosition(Vector3 chunkPosition, Vec3<byte> blockIndex)
    {
        return new Vector3(blockIndex.X, blockIndex.Y, blockIndex.Z) + chunkPosition;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Chunk other) return false;
        return this == other;
    }

    public override int GetHashCode()
    {
        return Index.GetHashCode();
    }
}
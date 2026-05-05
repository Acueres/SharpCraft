using System;
using System.Collections.Concurrent;

using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.Persistence;
using SharpCraft.MathUtilities;
using Microsoft.Xna.Framework;

namespace SharpCraft.World.Generation;

class ChunkGenerator
{
    readonly int seed;
    readonly ChunkPersistenceService chunkPersistence;
    readonly BlockMetadataProvider blockMetadata;

    readonly TopographyGenerator topographyGenerator;
    readonly BiomeMapGenerator biomeMapGenerator;
    readonly GeologyGenerator geologyGenerator;

    readonly ConcurrentDictionary<Vec2<int>, int[,]> heightLevelCache = [];
    readonly ConcurrentDictionary<Vec2<int>, int[,]> waterLevelCache = [];
    readonly ConcurrentDictionary<Vec2<int>, ReliefType[,]> terrainCache = [];
    readonly ConcurrentDictionary<Vec2<int>, int> maxElevationCache = [];

    public ChunkGenerator(Parameters parameters, ChunkPersistenceService chunkPersistence,
        BlockMetadataProvider blockMetadata)
    {
        this.blockMetadata = blockMetadata;
        this.chunkPersistence = chunkPersistence;

        seed = parameters.Seed;

        topographyGenerator = new(seed);
        biomeMapGenerator = new(seed);
        geologyGenerator = new(blockMetadata);
    }

    public Chunk GenerateChunk(Vec3<int> index)
    {
        Chunk chunk = new(index, blockMetadata);

        Vec2<int> cacheIndex = new(index.X, index.Z);
        
        if (index == new Vec3<int>(0, 16, -2))
        {
            int f = 0;
        }

        // Load chunk from disk, if exists
        if (chunkPersistence.TryLoadChunk(index, out var buffer))
        {
            chunk.BuildPalette(buffer);
            SeedLightSourcesFromBuffer(chunk, buffer);
            AdjustMaximumElevation(chunk, cacheIndex);
            return chunk;
        }
        
        int chunkSeed = HashCode.Combine(index.X, index.Y, index.Z, seed);
        Random rnd = new(chunkSeed);

        TopographyData topographyData;
        if (heightLevelCache.TryGetValue(cacheIndex, out int[,] heightValue)
            && waterLevelCache.TryGetValue(cacheIndex, out int[,] waterLevelValue)
            && terrainCache.TryGetValue(cacheIndex, out var terrainValue)
            && maxElevationCache.TryGetValue(cacheIndex, out var maxElevationValue))
        {
            topographyData = new TopographyData(heightValue, waterLevelValue, terrainValue, maxElevationValue);
        }
        else
        {
            topographyData = topographyGenerator.GetTopographyData(chunk.Position);

            heightLevelCache.TryAdd(cacheIndex, topographyData.HeightLevel);
            waterLevelCache.TryAdd(cacheIndex, topographyData.WaterLevel);
            terrainCache.TryAdd(cacheIndex, topographyData.ReliefData);
            maxElevationCache.TryAdd(cacheIndex, topographyData.MaxElevation);
        }

        int maxElevation = topographyData.MaxElevation;
        var heightLevel = topographyData.HeightLevel;
        var waterLevel = topographyData.WaterLevel;
        var terrain = topographyData.ReliefData;
        
        if (chunk.Index.Y * Chunk.Size > maxElevation)
        {
            return chunk;
        }

        buffer = Chunk.GetBlockArray();

        for (int x = 0; x < Chunk.Size; x++)
        for (int z = 0; z < Chunk.Size; z++)
        for (int y = 0; y < Chunk.Size; y++)
        {
            int currentY = (int)chunk.Position.Y + y;

            ushort texture = geologyGenerator.GetBlockForLayer(
                heightLevel[x, z],
                currentY,
                waterLevel[x, z],
                terrain[x, z],
                rnd
            );

            if (texture != Block.EmptyValue)
                buffer[x, y, z] = new(texture);
        }

        chunk.BuildPalette(buffer);
        SeedLightSourcesFromBuffer(chunk, buffer);

        return chunk;
    }

    public int GetTerrainHeight(int worldX, int worldZ)
    {
        var (chunkX, blockX) = SplitWorldCoord(worldX);
        var (chunkZ, blockZ) = SplitWorldCoord(worldZ);

        EnsureTopographyCached(chunkX, chunkZ);

        return heightLevelCache[new Vec2<int>(chunkX, chunkZ)][blockX, blockZ];
    }

    public int GetWaterHeight(int worldX, int worldZ)
    {
        var (chunkX, blockX) = SplitWorldCoord(worldX);
        var (chunkZ, blockZ) = SplitWorldCoord(worldZ);

        EnsureTopographyCached(chunkX, chunkZ);

        return waterLevelCache[new Vec2<int>(chunkX, chunkZ)][blockX, blockZ];
    }

    void EnsureTopographyCached(int chunkX, int chunkZ)
    {
        Vec2<int> cacheIndex = new(chunkX, chunkZ);

        if (heightLevelCache.ContainsKey(cacheIndex)
            && waterLevelCache.ContainsKey(cacheIndex)
            && terrainCache.ContainsKey(cacheIndex)
            && maxElevationCache.ContainsKey(cacheIndex))
        {
            return;
        }

        Vector3 chunkWorldPos = new(chunkX * Chunk.Size, 0, chunkZ * Chunk.Size);
        TopographyData topographyData = topographyGenerator.GetTopographyData(chunkWorldPos);

        heightLevelCache.TryAdd(cacheIndex, topographyData.HeightLevel);
        waterLevelCache.TryAdd(cacheIndex, topographyData.WaterLevel);
        terrainCache.TryAdd(cacheIndex, topographyData.ReliefData);
        maxElevationCache.TryAdd(cacheIndex, topographyData.MaxElevation);
    }

    static (int chunk, int block) SplitWorldCoord(int worldCoord)
    {
        int chunk = FloorDiv(worldCoord, Chunk.Size);
        int block = PositiveMod(worldCoord, Chunk.Size);
        return (chunk, block);
    }

    static int FloorDiv(int value, int divisor)
    {
        int q = value / divisor;
        int r = value % divisor;

        if (r != 0 && ((r > 0) != (divisor > 0)))
            q--;

        return q;
    }

    static int PositiveMod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private void SeedLightSourcesFromBuffer(Chunk chunk, Block[,,] buffer)
    {
        if (buffer is null) return;

        for (byte y = 0; y < Chunk.Size; y++)
        for (byte x = 0; x < Chunk.Size; x++)
        for (byte z = 0; z < Chunk.Size; z++)
        {
            Block b = buffer[x, y, z];
            if (!b.IsEmpty && blockMetadata.IsLightSource(b))
                chunk.AddLightSource(x, y, z, b);
        }
    }

    public bool IsSunlight(Chunk chunk)
    {
        int maxElevation = maxElevationCache[new Vec2<int>(chunk.Index.X, chunk.Index.Z)];
        int y = Chunk.WorldToChunkIndex(maxElevation);
        return chunk.Index.Y == y || chunk.Index.Y == y + 1;
    }

    public ReliefType GetReliefType(int cx, int cz, int bx, int bz)
    {
        return terrainCache[new Vec2<int>(cx, cz)][bx, bz];
    }

    public void RemoveCache(Vec3<int> index)
    {
        Vec2<int> cacheIndex = new(index.X, index.Z);
        heightLevelCache.TryRemove(cacheIndex, out _);
        waterLevelCache.TryRemove(cacheIndex, out _);
        terrainCache.TryRemove(cacheIndex, out _);
        maxElevationCache.TryRemove(cacheIndex, out _);
    }

    void AdjustMaximumElevation(Chunk chunk, Vec2<int> cacheIndex)
    {
        int? newMaxElevation = chunk.GetMaximumTerrainElevation();
        if (newMaxElevation.HasValue)
        {
            maxElevationCache[cacheIndex] = (int)newMaxElevation;
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.Persistence;
using System.Collections.Concurrent;
using SharpCraft.MathUtilities;

namespace SharpCraft.World.Generation;

public enum BiomeType : byte
{
    River,
    Forest,
    Mountain
}

class WorldGenerator
{
    readonly int seed;
    readonly DatabaseService db;
    readonly BlockMetadataProvider blockMetadata;

    readonly ConcurrentDictionary<Vector2I, int[,]> terrainLevelCache = [];
    readonly ConcurrentDictionary<Vector2I, BiomeType[,]> biomesCache = [];
    readonly ConcurrentDictionary<Vector2I, int> elevationCache = [];

    readonly int waterLevel;

    readonly FastNoiseLite terrain;
    readonly FastNoiseLite forest;
    readonly FastNoiseLite mountain;
    readonly FastNoiseLite river;

    readonly ushort bedrock, grass, stone, dirt, snow,
           leaves, birch, oak, water,
           sand, sandstone;


    public WorldGenerator(Parameters parameters, DatabaseService databaseService, BlockMetadataProvider blockMetadata)
    {
        this.blockMetadata = blockMetadata;
        db = databaseService;

        waterLevel = 40;

        seed = parameters.Seed;

        bedrock = blockMetadata.GetBlockIndex("bedrock");
        grass = blockMetadata.GetBlockIndex("grass_side");
        stone = blockMetadata.GetBlockIndex("stone");
        dirt = blockMetadata.GetBlockIndex("dirt");
        snow = blockMetadata.GetBlockIndex("snow");
        leaves = blockMetadata.GetBlockIndex("leaves");
        birch = blockMetadata.GetBlockIndex("birch_log");
        oak = blockMetadata.GetBlockIndex("oak_log");
        water = blockMetadata.GetBlockIndex("water");
        sand = blockMetadata.GetBlockIndex("sand");
        sandstone = blockMetadata.GetBlockIndex("sandstone_top");

        terrain = new FastNoiseLite(seed);
        terrain.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        terrain.SetFrequency(0.001f);

        forest = new FastNoiseLite(seed);
        forest.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        forest.SetFractalType(FastNoiseLite.FractalType.Ridged);

        mountain = new FastNoiseLite(seed);
        mountain.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        mountain.SetFractalType(FastNoiseLite.FractalType.Ridged);
        mountain.SetFrequency(0.005f);

        river = new FastNoiseLite(seed);
        river.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        river.SetFractalType(FastNoiseLite.FractalType.Ridged);
        river.SetFrequency(0.001f);
    }

    public void ClearCache()
    {
        terrainLevelCache.Clear();
        biomesCache.Clear();
        elevationCache.Clear();

    }

    public (Chunk, ChunkBuffer) GenerateChunk(Vector3I index)
    {
        Chunk chunk = new(index, blockMetadata);
        Block[,,] blocks = null;
        int chunkSeed = HashCode.Combine(index.X, index.Y, index.Z, seed);
        Random rnd = new(chunkSeed);

        int maxElevation = int.MinValue;
        Vector2I cacheIndex = new(index.X, index.Z);
        BiomeType[,] biomes;
        if (!terrainLevelCache.TryGetValue(cacheIndex, out int[,] terrainLevel))
        {
            terrainLevel = new int[Chunk.Size, Chunk.Size];
            biomes = new BiomeType[Chunk.Size, Chunk.Size];

            for (int x = 0; x < Chunk.Size; x++)
            {
                for (int z = 0; z < Chunk.Size; z++)
                {
                    (int height, BiomeType biome) = GetHeight(chunk.Position, x, z);
                    terrainLevel[x, z] = height;
                    biomes[x, z] = biome;

                    if (maxElevation < height)
                    {
                        maxElevation = height;
                    }
                }
            }

            terrainLevelCache.TryAdd(cacheIndex, terrainLevel);
            biomesCache.TryAdd(cacheIndex, biomes);
        }
        else
        {
            maxElevation = (from int h in terrainLevel select h).Max();
            biomes = biomesCache[cacheIndex];
        }

        elevationCache.TryAdd(cacheIndex, maxElevation);

        if (maxElevation < chunk.Position.Y)
        {
            blocks = db.ApplyDelta(chunk, blocks);
            chunk.Init(blocks);
            return (chunk, new(blocks));
        }

        blocks = Chunk.GetBlockArray();

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                if (chunk.Position.Y > terrainLevel[x, z]) continue;

                float diff = terrainLevel[x, z] - chunk.Position.Y;
                int yMax = Math.Clamp((int)diff, 0, Chunk.Size);

                for (int y = 0; y < yMax; y++)
                {
                    ushort texture = Fill(terrainLevel[x, z], (int)chunk.Position.Y + y, biomes[x, z], rnd);
                    blocks[x, y, z] = new(texture);
                }
            }
        }

        db.ApplyDelta(chunk, blocks);

        chunk.Init(blocks);

        return (chunk, new(blocks));
    }

    public List<Vector3I> GetSkyLevel()
    {
        List<Vector3I> indexes = new(elevationCache.Count);
        foreach ((Vector2I index, int value) in elevationCache)
        {
            int y = Chunk.WorldToChunkIndex(value) + 1;
            indexes.Add(new Vector3I(index.X, y, index.Z));
        }

        return indexes;
    }

    (int, BiomeType) GetHeight(Vector3 position, int x, int z)
    {
        float xVal = x + position.X;
        float zVal = z + position.Z;

        int height;
        BiomeType biome;
        float noise = Math.Abs(terrain.GetNoise(xVal, zVal) + 0.5f);

        if (noise < 0.05f)
        {
            height = (int)Math.Abs(8 * (river.GetNoise(xVal, zVal) + 0.8f)) + 30;
            biome = BiomeType.River;
        }
        else if (noise < 1.2f)
        {
            height = (int)Math.Abs(10 * (forest.GetNoise(xVal, zVal) + 0.8f)) + 30;
            biome = BiomeType.Forest;
        }
        else
        {
            height = (int)Math.Abs(30 * (mountain.GetNoise(xVal, zVal) + 0.8f)) + 30;
            biome = BiomeType.Mountain;
        }

        return (height, biome);
    }

    ushort Fill(int terrainHeight, int currentY, BiomeType biome, Random rnd)
    {
        switch (biome)
        {
            case BiomeType.River:
                {
                    if (currentY < -100)
                    {
                        return bedrock;
                    }

                    if (currentY == terrainHeight - 1)
                    {
                        return sand;
                    }
                    else if (currentY > terrainHeight - 6)
                    {
                        if (rnd.Next(0, 2) == 0)
                            return sandstone;
                        return sand;
                    }

                    ushort texture = stone;

                    if (rnd.Next(0, 2) == 0)
                        texture = dirt;

                    return texture;
                }

            case BiomeType.Forest:
                {
                    if (currentY < -100)
                    {
                        return bedrock;
                    }

                    if (currentY == terrainHeight - 1)
                    {
                        return grass;
                    }

                    ushort texture = stone;

                    if (rnd.Next(0, 2) == 0)
                        texture = dirt;

                    return texture;
                }

            case BiomeType.Mountain:
                {
                    if (currentY < -100)
                    {
                        return bedrock;
                    }

                    if (currentY < 50 && currentY == terrainHeight - 1)
                    {
                        return grass;
                    }

                    if (currentY >= 60 && currentY == terrainHeight - 1)
                    {
                        return snow;
                    }

                    return stone;
                }
        }

        return 0;
    }
}

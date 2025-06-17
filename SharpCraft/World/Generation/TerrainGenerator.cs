using System;
using Microsoft.Xna.Framework;

using SharpCraft.MathUtilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;

namespace SharpCraft.World.Generation;

public enum BiomeType
{
    Plain,
    Mountain,
    River
}

record TerrainData(int[,] TerrainLevel, int[,] WaterLevel, BiomeType[,] BiomesData, int MaxElevation);

class TerrainGenerator
{
    readonly int seed;

    readonly FastNoiseLite continental, erosion, peaksValleys, riverNoise;
    readonly FastNoiseLite warpX, warpZ, temperature, humidity;

    readonly ushort bedrock, grass, stone, dirt, snow,
           leaves, birch, oak, water,
           sand, sandstone;

    const int seaLevel = 0;
    const int snowLevel = 150;
    const int dirtLayerDepth = 4;

    public TerrainGenerator(int seed, BlockMetadataProvider blockMetadata)
    {
        this.seed = seed;

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

        continental = MakeNoise(1, 0.0002f, FastNoiseLite.FractalType.FBm, 3);
        erosion = MakeNoise(2, 0.0010f, FastNoiseLite.FractalType.FBm, 4);
        peaksValleys = MakeNoise(3, 0.0015f, FastNoiseLite.FractalType.Ridged, 5);
        riverNoise = MakeNoise(4, 0.0015f, FastNoiseLite.FractalType.Ridged, 1);
        temperature = MakeNoise(5, 0.0020f, FastNoiseLite.FractalType.FBm, 3);
        humidity = MakeNoise(6, 0.0020f, FastNoiseLite.FractalType.FBm, 3);
        warpX = MakeNoise(1001, 0.0060f, FastNoiseLite.FractalType.DomainWarpProgressive, 3);
        warpZ = MakeNoise(1002, 0.0060f, FastNoiseLite.FractalType.DomainWarpProgressive, 3);
    }

    public TerrainData GenerateTerrainData(Vector3 position, Vec2<int> cacheIndex)
    {
        int maxElevation = int.MinValue;

        var terrainLevel = new int[Chunk.Size, Chunk.Size];
        var waterLevel = new int[Chunk.Size, Chunk.Size];
        var biomes = new BiomeType[Chunk.Size, Chunk.Size];

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                (int height, int waterLevelValue, BiomeType biome) = GetHeight(position, x, z);
                terrainLevel[x, z] = height;
                waterLevel[x, z] = waterLevelValue;
                biomes[x, z] = biome;

                if (maxElevation < height)
                {
                    maxElevation = height;
                }
            }
        }

        return new TerrainData(terrainLevel, waterLevel, biomes, maxElevation);
    }

    (int, int, BiomeType) GetHeight(Vector3 chunkPos, int x, int z)
    {
        // 1. Warp
        float gx = x + chunkPos.X;
        float gz = z + chunkPos.Z;

        const float warpScale = 25f;
        float wx = gx + warpX.GetNoise(gx, gz) * warpScale;
        float wz = gz + warpZ.GetNoise(gx, gz) * warpScale;

        // 2. Sample
        float continentalValue = continental.GetNoise(wx, wz);
        float erosionValue = erosion.GetNoise(wx, wz);
        float pvNoise = Math.Abs(peaksValleys.GetNoise(wx, wz));
        float pv = 1f - pvNoise; // pv is 1 in valley bottoms, 0 on ridges

        // 3. Base level & mountains
        float baseLevel = GetContinentalHeight(continentalValue);
        float mountainAmp = MathUtils.Lerp(5, 120, MathUtils.InverseLerp(-0.6f, 0.5f, erosionValue));
        float mountainsMask = MathUtils.SmoothStep(1f - pv);
        float height = baseLevel + mountainAmp * mountainsMask;

        // 4. Biome assignment
        BiomeType biome;
        if (mountainsMask > 0.45f && erosionValue < 0.6f)
        {
            biome = BiomeType.Mountain;
        }
        else
        {
            biome = BiomeType.Plain;
        }

        // 5. River generation & water level
        // Define the conditions for a river to form
        // We want deep valleys (high pv) in low-erosion areas
        const float riverValleyThreshold = 0.95f; // How deep a valley must be (0 to 1)
        const float riverErosionThreshold = -0.1f; // How low erosion must be (-1 to 1)

        bool isPotentialRiverLocation = pv > riverValleyThreshold && erosionValue < riverErosionThreshold;

        int terrainHeight;
        int waterLevel;

        if (isPotentialRiverLocation)
        {
            const int maxRiverDepth = 8;
            const int waterSurfaceOffset = 1;

            int waterSurfaceLevel = (int)Math.Floor(height) - waterSurfaceOffset;

            float riverT = MathUtils.InverseLerp(riverValleyThreshold, 1.0f, pv);
            float channelProfile = riverT * riverT;
            int depthBelowWaterSurface = (int)Math.Ceiling(channelProfile * maxRiverDepth);

            if (depthBelowWaterSurface == 0 && channelProfile > 0)
            {
                depthBelowWaterSurface = 1;
            }

            terrainHeight = waterSurfaceLevel - depthBelowWaterSurface;
            waterLevel = waterSurfaceLevel;

            biome = BiomeType.River;
        }
        else
        {
            terrainHeight = (int)MathF.Floor(height);
            waterLevel = seaLevel;
        }

        return (terrainHeight, waterLevel, biome);
    }

    public ushort Fill(int terrainHeight, int currentY, int waterLevel, BiomeType biome, Random rnd)
    {
        // Handle everything above the solid ground
        if (currentY >= terrainHeight)
        {
            return currentY < waterLevel ? water : Block.EmptyValue;
        }

        // Handle the solid ground itself
        if (currentY < 5)
        {
            return bedrock;
        }

        int depthFromSurface = (terrainHeight - 1) - currentY;
        bool isSurface = depthFromSurface == 0;

        switch (biome)
        {
            case BiomeType.Plain:
                if (isSurface)
                {
                    if (currentY >= snowLevel) return snow;
                    return grass;
                }
                if (depthFromSurface <= dirtLayerDepth)
                {
                    return dirt;
                }
                return stone;

            case BiomeType.Mountain:
                if (isSurface)
                {
                    if (currentY >= snowLevel) return snow;
                    if (currentY > 80 && rnd.Next(0, 3) > 0) return stone;
                    if (currentY > 90 && rnd.Next(0, 2) > 0) return stone;
                    if (currentY > 100 && rnd.Next(0, 1) > 0) return stone;
                    if (currentY > 110) return stone;
                    return grass;
                }
                if (depthFromSurface <= 2)
                {
                    return dirt;
                }
                return stone;

            case BiomeType.River:
                if (depthFromSurface <= dirtLayerDepth)
                {
                    return rnd.Next(0, 4) == 0 ? dirt : sand;
                }
                return stone;
        }

        return stone;
    }

    static float GetContinentalHeight(float cVal)
    {
        ReadOnlySpan<float> xs = [-1f, -0.2f, 0.4f, 1f];
        ReadOnlySpan<float> ys = [20f, 60f, 110f, 140f];

        // Choose the spline segment
        int seg;
        // deep-ocean to coast
        if (cVal < xs[1]) seg = 0;
        // coast to lowland
        else if (cVal < xs[2]) seg = 1;
        // lowland to highland
        else seg = 2;

        // Local parameter 0-1 within that segment
        float t = MathUtils.InverseLerp(xs[seg], xs[seg + 1], cVal);

        // End-point heights for Catmull
        // Clamp indices at the ends of the array
        float y0 = ys[Math.Max(seg - 1, 0)];
        float y1 = ys[seg];
        float y2 = ys[seg + 1];
        float y3 = ys[Math.Min(seg + 2, ys.Length - 1)];

        return MathUtils.Catmull(y0, y1, y2, y3, t);
    }

    FastNoiseLite MakeNoise(int offset, float freq, FastNoiseLite.FractalType ft, int oct)
    {
        var noise = new FastNoiseLite(seed + offset);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(freq);
        noise.SetFractalType(ft);
        noise.SetFractalOctaves(oct);
        return noise;
    }
}

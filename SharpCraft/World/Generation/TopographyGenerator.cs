using System;
using Microsoft.Xna.Framework;

using SharpCraft.MathUtilities;
using SharpCraft.World.Chunks;

namespace SharpCraft.World.Generation;

public enum ReliefType
{
    Ocean,
    Coast,
    Plain,
    Highland,
    Mountain,
    RiverValley
}

public record TopographyData(
    int[,] TerrainLevel,
    int[,] WaterLevel,
    ReliefType[,] ReliefData,
    int MaxElevation);

record TerrainShapeData(
    int TerrainHeight,
    int WaterLevel,
    ReliefType ReliefType
);

public class TopographyGenerator(int seed)
{
    readonly FastNoiseLite continental = FastNoiseLite.GetNoise(seed + 1, 0.0002f, FastNoiseLite.FractalType.FBm, 3);
    readonly FastNoiseLite erosion = FastNoiseLite.GetNoise(seed + 2, 0.0010f, FastNoiseLite.FractalType.FBm, 4);
    readonly FastNoiseLite peaksValleys = FastNoiseLite.GetNoise(seed + 3, 0.0015f, FastNoiseLite.FractalType.Ridged, 5);
    readonly FastNoiseLite warpX = FastNoiseLite.GetNoise(seed + 1001, 0.0060f, FastNoiseLite.FractalType.DomainWarpProgressive, 3);
    readonly FastNoiseLite warpZ = FastNoiseLite.GetNoise(seed + 1002, 0.0060f, FastNoiseLite.FractalType.DomainWarpProgressive, 3);

    const int seaLevel = 0;

    public TopographyData GetTopographyData(Vector3 position)
    {
        int maxElevation = int.MinValue;

        var terrainLevel = new int[Chunk.Size, Chunk.Size];
        var waterLevel = new int[Chunk.Size, Chunk.Size];
        var reliefData = new ReliefType[Chunk.Size, Chunk.Size];

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                (int height, int waterLevelValue, ReliefType biome) = GetTerrainShapeData(position, x, z);
                terrainLevel[x, z] = height;
                waterLevel[x, z] = waterLevelValue;
                reliefData[x, z] = biome;

                if (maxElevation < height)
                {
                    maxElevation = height;
                }
            }
        }

        return new TopographyData(terrainLevel, waterLevel, reliefData, maxElevation);
    }

    TerrainShapeData GetTerrainShapeData(Vector3 pos, int x, int z)
    {
        // 1. Warp
        float gx = x + pos.X;
        float gz = z + pos.Z;

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
        ReliefType biome;
        if (mountainsMask > 0.45f && erosionValue < 0.6f)
        {
            biome = ReliefType.Mountain;
        }
        else
        {
            biome = ReliefType.Plain;
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

            biome = ReliefType.RiverValley;
        }
        else
        {
            terrainHeight = (int)MathF.Floor(height);
            waterLevel = seaLevel;
        }

        return new(terrainHeight, waterLevel, biome);
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
}

using System;
using Microsoft.Xna.Framework;

using SharpCraft.MathUtilities;
using SharpCraft.World.Chunks;

namespace SharpCraft.World.Generation;

public enum ReliefType
{
    Sea,
    Coast,
    Plateau,
    Highland,
    Mountain,
    RiverValley
}

public record TopographyData(
    int[,] HeightLevel,
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
    readonly FastNoiseLite peaksValleys = FastNoiseLite.GetNoise(seed + 3, 0.0020f, FastNoiseLite.FractalType.Ridged, 5);
    readonly FastNoiseLite warpX = FastNoiseLite.GetNoise(seed + 1001, 0.0060f, FastNoiseLite.FractalType.DomainWarpProgressive, 3);
    readonly FastNoiseLite warpZ = FastNoiseLite.GetNoise(seed + 1002, 0.0060f, FastNoiseLite.FractalType.DomainWarpProgressive, 3);

    readonly float[] splineXs = [-1.0f, 0.3f, 0.5f, 1.0f];
    readonly float[] splineYs = [160f, 150f, -50f, 130f];

    const int seaLevel = 0;
    const int inlandSeaLevel = 60;

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
        float pv = 1f - pvNoise;

        // 3. Base level & mountains
        float baseLevel = GetContinentalHeight(continentalValue);

        float mountainAmp = MathUtils.Lerp(30, 250, MathUtils.InverseLerp(-0.6f, 0.5f, erosionValue));
        float mountainsMask = MathUtils.SmoothStep(1f - pv);
        float height = baseLevel + mountainAmp * mountainsMask;

        int terrainHeight;
        int waterLevel;
        ReliefType relief;

        // 4. Check for rivers
        const float riverValleyThreshold = 0.95f;
        const float riverErosionThreshold = -0.1f;
        bool isPotentialRiverLocation = pv > riverValleyThreshold && erosionValue < riverErosionThreshold;

        if (isPotentialRiverLocation)
        {
            const int maxRiverDepth = 8;
            const int waterSurfaceOffset = 1;
            int waterSurfaceLevel = (int)Math.Floor(height) - waterSurfaceOffset;

            float riverT = MathUtils.InverseLerp(riverValleyThreshold, 1.0f, pv);
            float channelProfile = riverT * riverT;
            int depthBelowWaterSurface = (int)Math.Ceiling(channelProfile * maxRiverDepth);

            if (depthBelowWaterSurface == 0 && channelProfile > 0) depthBelowWaterSurface = 1;

            terrainHeight = waterSurfaceLevel - depthBelowWaterSurface;
            waterLevel = waterSurfaceLevel;
            relief = ReliefType.RiverValley;
        }
        else
        {
            // 5. Check for inland seas
            terrainHeight = (int)MathF.Floor(height);

            if (terrainHeight < inlandSeaLevel)
            {
                waterLevel = inlandSeaLevel;
                relief = (terrainHeight > inlandSeaLevel - 10) ? ReliefType.Coast : ReliefType.Sea;
            }
            else
            {
                // 6. If not a river or sea, it's dry land
                waterLevel = seaLevel;

                if (mountainsMask > 0.45f && erosionValue < 0.6f)
                {
                    relief = ReliefType.Mountain;
                }
                else
                {
                    relief = ReliefType.Plateau;
                }
            }
        }

        return new(terrainHeight, waterLevel, relief);
    }

    float GetContinentalHeight(float cVal)
    {
        // Choose the spline segment
        int seg;
        // High plateau
        if (cVal < splineXs[1]) seg = 0;
        // Plateau edge to deep sea bottom
        else if (cVal < splineXs[2]) seg = 1;
        // Sea Bottom to lower hills
        else seg = 2;

        // Local parameter 0-1 within that segment
        float t = MathUtils.InverseLerp(splineXs[seg], splineXs[seg + 1], cVal);

        // End-point heights for Catmull
        // Clamp indices at the ends of the array
        float y0 = splineYs[Math.Max(seg - 1, 0)];
        float y1 = splineYs[seg];
        float y2 = splineYs[seg + 1];
        float y3 = splineYs[Math.Min(seg + 2, splineYs.Length - 1)];

        return MathUtils.Catmull(y0, y1, y2, y3, t);
    }
}

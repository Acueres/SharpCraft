using SharpCraft.World.Blocks;
using System;

namespace SharpCraft.World.Generation;

public class GeologyGenerator
{
    readonly ushort bedrock, grass, stone, dirt, snow,
           leaves, birch, oak, water,
           sand, sandstone;

    const int snowLevel = 150;
    const int dirtLayerDepth = 4;

    public GeologyGenerator(BlockMetadataProvider blockMetadata)
    {
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
    }

    public ushort GetBlockForLayer(int terrainHeight, int currentY, int waterLevel, ReliefType biome, Random rnd)
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
            case ReliefType.Plain:
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

            case ReliefType.Mountain:
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

            case ReliefType.RiverValley:
                if (depthFromSurface <= dirtLayerDepth)
                {
                    return rnd.Next(0, 4) == 0 ? dirt : sand;
                }
                return stone;
        }

        return stone;
    }
}

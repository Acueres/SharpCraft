using System;
using Microsoft.Xna.Framework;

using SharpCraft.Utility;

namespace SharpCraft.World
{
    public enum BiomeType : byte
    {
        River,
        Forest,
        Mountain
    }

    public class WorldGenerator
    {
        readonly int size;
        readonly string type;

        readonly int waterLevel;
        readonly int last;

        readonly FastNoiseLite terrain;
        readonly FastNoiseLite forest;
        readonly FastNoiseLite mountain;
        readonly FastNoiseLite river;

        readonly ushort bedrock, grass, stone, dirt, snow,
               granite, leaves, birch, oak, water,
               sand, sandstone;

        readonly BlockMetadataProvider blockMetadata;


        public WorldGenerator(Parameters parameters, BlockMetadataProvider blockMetadata)
        {
            this.blockMetadata = blockMetadata;
            size = Chunk.SIZE;
            type = parameters.WorldType;

            waterLevel = 40;
            last = size - 1;

            int seed = parameters.Seed;

            bedrock = blockMetadata.GetBlockIndex("bedrock");
            grass = blockMetadata.GetBlockIndex("grass_side");
            stone = blockMetadata.GetBlockIndex("stone");
            dirt = blockMetadata.GetBlockIndex("dirt");
            snow = blockMetadata.GetBlockIndex("snow");
            granite = blockMetadata.GetBlockIndex("granite");
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

        public Chunk GenerateChunk(Vector3I position)
        {
            return type switch
            {
                "Flat" => Flat(position),
                _ => Default(position),
            };
        }
        
        Chunk Default(Vector3I index)
        {
            Chunk chunk = new(index, blockMetadata);
            Random rnd = new(index.GetHashCode());

            int[,] elevationMap = new int[size, size];

            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    (int height, BiomeType biome) = GetHeight(chunk.Position, x, z);
                    elevationMap[x, z] = height;
                    chunk.Biomes[x][z] = biome;
                }
            }

            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    for (int y = 0; y < elevationMap[x, z]; y++)
                    {
                        ushort texture = Fill(elevationMap[x, z], y, chunk.Biomes[x][z], rnd);

                        chunk[x, y, z] = new(texture);
                    }

                    if (chunk.Biomes[x][z] == 0)
                    {
                        for (int i = elevationMap[x, z]; i < waterLevel; i++)
                        {
                            chunk[x, i, z] = new(water);
                        }
                    }
                }
            }

            GenerateTrees(chunk, elevationMap, rnd);

            return chunk;
        }

        (int, BiomeType) GetHeight(Vector3 position, int x, int z)
        {
            float xVal = x - position.X;
            float zVal = z - position.Z;

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

        Chunk Flat(Vector3I position)
        {
            Chunk chunk = new(position, blockMetadata);
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        ushort texture;

                        if (y == 0)
                            texture = bedrock;
                        else if (y == 4)
                            texture = grass;
                        else
                            texture = dirt;

                        chunk[x, y, z] = new(texture);
                    }
                }
            }

            return chunk;
        }

        ushort Fill(int maxY, int currentY, BiomeType biome, Random rnd)
        {
            switch (biome)
            {
                case BiomeType.River:
                    {
                        if (currentY == 0)
                        {
                            return bedrock;
                        }

                        if (currentY == maxY - 1)
                        {
                            return sand;
                        }
                        else if (currentY > maxY - 6)
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
                        if (currentY == 0)
                        {
                            return bedrock;
                        }

                        if (currentY == maxY - 1)
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
                        if (currentY == 0)
                        {
                            return bedrock;
                        }

                        if (currentY < 50 && currentY == maxY - 1)
                        {
                            return grass;
                        }

                        if (currentY >= 60 && currentY == maxY - 1)
                        {
                            return snow;
                        }

                        return stone;
                    }
            }

            return 0;
        }

        void GenerateTrees(Chunk chunk, int[,] elevationMap, Random rnd)
        {
            int n = rnd.Next(2, 6);
            int[,] coords = new int[n, 2];

            int i = 0;

            while (i < n)
            {
                int x = rnd.Next(1, last - 1);
                int z = rnd.Next(1, last - 1);

                bool skip = false;
                for (int j = 0; j < i; j++)
                {
                    if (Math.Abs(coords[j, 0] - x) < 2 ||
                        Math.Abs(coords[j, 1] - z) < 2)
                    {
                        skip = true;
                        break;
                    }
                }

                if (skip)
                {
                    continue;
                }

                if (chunk.Biomes[x][z] == (byte)BiomeType.River ||
                    elevationMap[x, z] > 50)
                {
                    i++;
                    continue;
                }

                coords[i, 0] = x;
                coords[i, 1] = z;

                i++;
            }

            for (i = 0; i < n; i++)
            {
                int x = coords[i, 0];
                int z = coords[i, 1];

                if (x == 0 || z == 0)
                    break;

                int y = elevationMap[x, z];

                ushort wood;

                if (rnd.Next(0, 5) == 4)
                    wood = birch;
                else
                    wood = oak;

                MakeTree(chunk, wood, y, x, z, rnd);
            }
        }

        void MakeTree(Chunk chunk, ushort wood, int y, int x, int z, Random rnd)
        {
            if (chunk[x, y - 1, z].Value != grass &&
                chunk[x, y - 1, z].Value != dirt)
            {
                return;
            }

            int height = rnd.Next(3, 6);
            int level1 = y + height - 1;
            int level2 = y + height;
            int level3 = y + height + 1;

            for (int i = y; i < y + height; i++)
            {
                chunk[x, i, z] = new(wood);
            }

            for (int i = x - 1; i < x + 2; i++)
            {
                for (int j = z - 1; j < z + 2; j++)
                {
                    if (chunk[i, level1, j].IsEmpty)
                    {
                        chunk[i, level1, j] = new(leaves);
                    }
                }
            }

            if (chunk[x, level2, z].IsEmpty)
            {
                chunk[x, level2, z] = new(leaves);
            }
            if (chunk[x, level3, z].IsEmpty)
            {
                chunk[x, level3, z] = new(leaves);
            }

            if (chunk[x - 1, level2, z].IsEmpty)
            {
                chunk[x - 1, level2, z] = new(leaves);
            }
            if (chunk[x + 1, level2, z].IsEmpty)
            {
                chunk[x + 1, level2, z] = new(leaves);
            }

            if (chunk[x, level2, z - 1].IsEmpty)
            {
                chunk[x, level2, z - 1] = new(leaves);
            }
            if (chunk[x, level2, z + 1].IsEmpty)
            {
                chunk[x, level2, z + 1] = new(leaves);
            }
        }
    }
}
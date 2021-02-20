using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;


namespace SharpCraft
{
    class WorldGenerator
    {
        int size;
        string type;

        int waterLevel;
        int last;

        FastNoiseLite terrain;
        FastNoiseLite forest;
        FastNoiseLite mountain;
        FastNoiseLite river;

        enum Biomes: byte
        {
            River,
            Forest,
            Mountain
        }

        Random rnd;

        ushort bedrock, grass, stone, dirt, snow,
               granite, leaves, birch, oak, water,
               sand, sandstone;


        public WorldGenerator(Parameters parameters)
        {
            size = Settings.ChunkSize;
            type = parameters.WorldType;

            waterLevel = 40;
            last = size - 1;

            int seed = parameters.Seed;

            var blockIndices = Assets.BlockIndices;

            bedrock = blockIndices["Bedrock"];
            grass = blockIndices["Grass"];
            stone = blockIndices["Stone"];
            dirt = blockIndices["Dirt"];
            snow = blockIndices["Snow Block"];
            granite = blockIndices["Granite"];
            leaves = blockIndices["Leaves"];
            birch = blockIndices["Birch Log"];
            oak = blockIndices["Oak Log"];
            water = blockIndices["Water"];
            sand = blockIndices["Sand"];
            sandstone = blockIndices["Sandstone"];

            rnd = new Random(seed);

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

        public Chunk GenerateChunk(Vector3 position)
        {
            return type switch
            {
                "Flat" => Flat(position),
                _ => Default(position),
            };
        }

        public ushort? Peek(Vector3 position, int y, int x, int z)
        {
            int height = GetHeight(position, x, z, out _);

            if (height > y)
            {
                return 0;
            }
            else
            {
                return null;
            }
        }
        
        Chunk Default(Vector3 position)
        {
            Chunk chunk = new Chunk(position, size: size);

            int[,] elevationMap = new int[size, size];

            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    elevationMap[x, z] = GetHeight(position, x, z, out byte biomeData);
                    chunk.BiomeData[x][z] = biomeData;
                }
            }

            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    for (int y = 0; y < elevationMap[x, z]; y++)
                    {
                        ushort texture = Fill(elevationMap[x, z], y, chunk.BiomeData[x][z]);

                        chunk.Blocks[y][x][z] = texture;
                    }

                    if (chunk.BiomeData[x][z] == 0)
                    {
                        for (int i = elevationMap[x, z]; i < waterLevel; i++)
                        {
                            chunk.Blocks[i][x][z] = water;
                        }
                    }
                }
            }

            GenerateTrees(chunk, elevationMap, 5);

            return chunk;
        }

        int GetHeight(Vector3 position, int x, int z, out byte biomeData)
        {
            float xVal = x - position.X;
            float zVal = z - position.Z;

            int height;
            float noise = Math.Abs(terrain.GetNoise(xVal, zVal) + 0.5f);

            if (noise < 0.05f)
            {
                height = (int)Math.Abs(8 * (river.GetNoise(xVal, zVal) + 0.8f)) + 30;
                biomeData = (byte)Biomes.River;
            }
            else if (noise < 1.2f)
            {
                height = (int)Math.Abs(10 * (forest.GetNoise(xVal, zVal) + 0.8f)) + 30;
                biomeData = (byte)Biomes.Forest;
            }
            else
            {
                height = (int)Math.Abs(30 * (mountain.GetNoise(xVal, zVal) + 0.8f)) + 30;
                biomeData = (byte)Biomes.Mountain;
            }

            return height;
        }

        Chunk Flat(Vector3 position)
        {
            Chunk chunk = new Chunk(position, size: size);

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

                        chunk.Blocks[y][x][z] = texture;
                    }
                }
            }

            return chunk;
        }

        ushort Fill(int maxY, int currentY, byte biome)
        {
            switch ((Biomes)biome)
            {
                case Biomes.River:
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

                case Biomes.Forest:
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

                case Biomes.Mountain:
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

        void GenerateTrees(Chunk chunk, int[,] elevationMap, int n)
        {
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

                if (chunk.BiomeData[x][z] == (byte)Biomes.River ||
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

                MakeTree(chunk, wood, y, x, z);
            }
        }

        void MakeTree(Chunk chunk, ushort wood, int y, int x, int z)
        {
            if (chunk.Blocks[y - 1][x][z] != grass &&
                chunk.Blocks[y - 1][x][z] != dirt)
            {
                return;
            }

            int height = rnd.Next(3, 6);
            int level1 = y + height - 1;
            int level2 = y + height;
            int level3 = y + height + 1;

            for (int i = y; i < y + height; i++)
            {
                chunk.Blocks[i][x][z] = wood;
            }

            for (int i = x - 1; i < x + 2; i++)
            {
                for (int j = z - 1; j < z + 2; j++)
                {
                    if (chunk.Blocks[level1][i][j] is null)
                    {
                        chunk.Blocks[level1][i][j] = leaves;
                    }
                }
            }

            if (chunk.Blocks[level2][x][z] is null)
            {
                chunk.Blocks[level2][x][z] = leaves;
            }
            if (chunk.Blocks[level3][x][z] is null)
            {
                chunk.Blocks[level3][x][z] = leaves;
            }

            if (chunk.Blocks[level2][x - 1][z] is null)
            {
                chunk.Blocks[level2][x - 1][z] = leaves;
            }
            if (chunk.Blocks[level2][x + 1][z] is null)
            {
                chunk.Blocks[level2][x + 1][z] = leaves;
            }

            if (chunk.Blocks[level2][x][z - 1] is null)
            {
                chunk.Blocks[level2][x][z - 1] = leaves;
            }
            if (chunk.Blocks[level2][x][z + 1] is null)
            {
                chunk.Blocks[level2][x][z + 1] = leaves;
            }
        }
    }
}
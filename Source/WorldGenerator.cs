using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    class WorldGenerator
    {
        int seed;
        int size;
        string Type;

        int waterLevel;
        int last;

        FastNoiseLite terrain;
        FastNoiseLite plains;
        FastNoiseLite mountains;
        FastNoiseLite rivers;

        Random rnd;

        ushort bedrock, grass, stone, dirt, snow,
               granite, leaves, birch, oak, water,
               sand, sandstone;


        public WorldGenerator(Dictionary<string, ushort> blockIndices)
        {
            seed = Parameters.Seed;
            size = Parameters.ChunkSize;
            Type = Parameters.WorldType;

            waterLevel = 40;
            last = size - 1;

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

            plains = new FastNoiseLite(seed);
            plains.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            plains.SetFractalType(FastNoiseLite.FractalType.Ridged);

            mountains = new FastNoiseLite(seed);
            mountains.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            mountains.SetFractalType(FastNoiseLite.FractalType.Ridged);
            mountains.SetFrequency(0.005f);

            rivers = new FastNoiseLite(seed);
            rivers.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            rivers.SetFractalType(FastNoiseLite.FractalType.Ridged);
            rivers.SetFrequency(0.001f);
        }

        public Chunk GenerateChunk(Vector3 position)
        {
            return Type switch
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

            GenerateTrees(chunk, elevationMap);

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
                height = (int)Math.Abs(8 * (rivers.GetNoise(xVal, zVal) + 0.8f)) + 30;
                biomeData = 0;
            }

            else if (noise < 1.2f)
            {
                height = (int)Math.Abs(10 * (plains.GetNoise(xVal, zVal) + 0.8f)) + 30;
                biomeData = 1;
            }

            else
            {
                height = (int)Math.Abs(30 * (mountains.GetNoise(xVal, zVal) + 0.8f)) + 30;
                biomeData = 2;
            }

            return height;
        }

        Chunk Flat(Vector3 position)
        {
            Chunk chunk = new Chunk(position, size: size);

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        ushort texture = grass;

                        if (y == 0)
                            texture = bedrock;

                        chunk.Blocks[y][x][z] = texture;
                    }
                }
            }

            return chunk;
        }

        ushort Fill(int maxY, int currentY, byte biome)
        {
            if (biome == 0)
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
            else if (biome == 1)
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
            else
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
        
        void GenerateTrees(Chunk chunk, int[,] elevationMap)
        {
            for (int i = 0; i < 3; i++)
            {
                int x = rnd.Next(0, size);
                int z = rnd.Next(0, size);

                if (chunk.BiomeData[x][z] == 0 ||
                    elevationMap[x, z] > 50)
                {
                    continue;
                }

                if (x < 3)
                    x += 2;
                else if (x > 13)
                    x -= 2;

                if (z < 3)
                    z += 2;
                else if (z > 13)
                    z -= 2;

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
            for (int i = y; i < y + 4; i++)
            {
                chunk.Blocks[i][x][z] = wood;
            }
            for (int i = y + 4; i < y + 7; i++)
            {
                chunk.Blocks[i][x][z] = leaves;
            }

            for (int h = y + 2; h < y + 5; h++)
            {
                for (int i = x - 1; i < x + 2; i++)
                {
                    for (int j = z - 1; j < z + 2; j++)
                    {
                        if ((i < size - 1 && i > 0) &&
                            (j < size - 1 && j > 0) &&
                            (h != y || i != x || j != z) &&
                            chunk.Blocks[h][i][j] is null)
                        {
                            chunk.Blocks[h][i][j] = leaves;
                        }
                    }
                }
            }
        }
    }
}
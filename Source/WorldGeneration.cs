using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SharpCraft
{
    class WorldGenerator
    {
        public int Seed;
        public int Size;

        string Type;

        int waterLevel;

        FastNoiseLite terrain;
        FastNoiseLite plains;
        FastNoiseLite mountains;
        FastNoiseLite rivers;

        Random rnd;

        ushort bedrock, grass, stone, dirt, snow,
               granite, leaves, birch, oak, water,
               sand, sandstone;


        public WorldGenerator(Dictionary<string, ushort> blockNames, string type = "flat", int size = 16, int seed = 0)
        {
            Seed = seed;
            Size = size;
            Type = type;

            waterLevel = 35;

            bedrock = blockNames["Bedrock"];
            grass = blockNames["Grass"];
            stone = blockNames["Stone"];
            dirt = blockNames["Dirt"];
            snow = blockNames["Snow Block"];
            granite = blockNames["Granite"];
            leaves = blockNames["Leaves"];
            birch = blockNames["Birch Log"];
            oak = blockNames["Oak Log"];
            water = blockNames["Water"];
            sand = blockNames["Sand"];
            sandstone = blockNames["Sandstone"];

            if (Seed == 0)
            {
                rnd = new Random();
                Seed = rnd.Next();
            }
            else
            {
                rnd = new Random(Seed);
            }

            terrain = new FastNoiseLite(Seed);
            terrain.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            //terrain.SetFractalType(FastNoiseLite.FractalType.PingPong);
            terrain.SetFrequency(0.001f);

            plains = new FastNoiseLite(Seed);
            plains.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            plains.SetFractalType(FastNoiseLite.FractalType.Ridged);
            //plains.SetFrequency(0.001f);

            mountains = new FastNoiseLite(Seed);
            mountains.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            mountains.SetFractalType(FastNoiseLite.FractalType.Ridged);
            mountains.SetFrequency(0.005f);

            rivers = new FastNoiseLite(Seed);
            rivers.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            rivers.SetFractalType(FastNoiseLite.FractalType.Ridged);
            rivers.SetFrequency(0.001f);
        }

        public Chunk GenerateChunk(Vector3 position)
        {
            switch (Type)
            {
                case "flat":
                    return Flat(position);

                default:
                    return Default(position);
            }
        }

        public ushort? LookUp(Vector3 position, int y, int x, int z)
        {
            byte b;
            int height = GetHeight(position, x, z, out b);

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
            Chunk chunk = new Chunk(position, size: Size);

            int[,] elevationMap = new int[Size, Size];

            for (int x = 0; x < Size; x++)
            {
                for (int z = 0; z < Size; z++)
                {
                    byte biomeData;
                    elevationMap[x, z] = GetHeight(position, x, z, out biomeData);
                    chunk.BiomeData[x][z] = biomeData;
                }
            }

            Interpolate(elevationMap);

            for (int x = 0; x < Size; x++)
            {
                for (int z = 0; z < Size; z++)
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
                height = (int)Math.Abs(8 * (rivers.GetNoise(xVal, zVal) + 0.8f)) + 25;
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
            Chunk chunk = new Chunk(position, size: Size);

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    for (int z = 0; z < Size; z++)
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

        void Interpolate(int[,] arr)
        {
            for (int x = 0; x < Size; x++)
            {
                for (int z = 0; z < Size; z++)
                {
                    for (int i = 5; i > 1; i--)
                    {
                        if (Size - z > i && arr[x, z] - arr[x, z + i] > 5)
                        {
                            arr[x, z + i - 1] = (arr[x, z] + arr[x, z + i]) / 2;
                        }

                        if (Size - x > i && arr[x, z] - arr[x + i, z] > 5)
                        {
                            arr[x + i - 1, z] = (arr[x, z] + arr[x + i, z]) / 2;
                        }
                    }
                }
            }
        }

        void GenerateTrees(Chunk chunk, int[,] elevationMap)
        {
            for (int i = 0; i < 3; i++)
            {
                int x = rnd.Next(0, Size);
                int z = rnd.Next(0, Size);

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
                        if ((i < Size - 1 && i > 0) &&
                            (j < Size - 1 && j > 0) &&
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
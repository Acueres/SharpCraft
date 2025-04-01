using System;
using System.Collections.Generic;

using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.Persistence;
using SharpCraft.MathUtilities;

namespace SharpCraft.World.Generation;

class WorldGenerator
{
    readonly int seed;
    readonly DatabaseService db;
    readonly BlockMetadataProvider blockMetadata;

    readonly TerrainGenerator terrainGenerator;

    public WorldGenerator(Parameters parameters, DatabaseService databaseService, BlockMetadataProvider blockMetadata)
    {
        this.blockMetadata = blockMetadata;
        db = databaseService;

        seed = parameters.Seed;

        terrainGenerator = new(seed, blockMetadata);
    }

    public Chunk GenerateChunk(Vec3<int> index)
    {
        Chunk chunk = new(index, blockMetadata);
        Block[,,] buffer = null;
        int chunkSeed = HashCode.Combine(index.X, index.Y, index.Z, seed);
        Vec2<int> cacheIndex = new(index.X, index.Z);
        Random rnd = new(chunkSeed);

        var terrainData = terrainGenerator.GenerateTerrainData(chunk.Position, cacheIndex);
        int maxElevation = terrainData.MaxElevation;
        var terrainLevel = terrainData.TerrainLevel;
        var biomes = terrainData.BiomesData;

        if (maxElevation < chunk.Index.Y * Chunk.Size)
        {
            buffer = db.ApplyDelta(chunk, buffer);
            chunk.Init(buffer);
            return chunk;
        }

        buffer = Chunk.GetBlockArray();

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                if (chunk.Position.Y > terrainLevel[x, z]) continue;

                float diff = terrainLevel[x, z] - chunk.Position.Y;
                int yMax = Math.Clamp((int)diff, 0, Chunk.Size);

                for (int y = 0; y < yMax; y++)
                {
                    ushort texture = terrainGenerator.Fill(terrainLevel[x, z], (int)chunk.Position.Y + y, biomes[x, z], rnd);
                    buffer[x, y, z] = new(texture);
                }
            }
        }

        db.ApplyDelta(chunk, buffer);

        chunk.Init(buffer);

        return chunk;
    }

    public List<Vec3<int>> GetSkyLevel()
    {
        return terrainGenerator.GetSkyLevel();
    }

    public void ClearCache()
    {
        terrainGenerator.ClearCache();
    }
}

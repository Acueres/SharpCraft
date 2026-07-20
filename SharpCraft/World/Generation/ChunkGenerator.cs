using SharpCraft.World.Chunks;
using SharpCraft.World.Blocks;
using SharpCraft.SharpMath;

using System.Collections.Concurrent;

namespace SharpCraft.World.Generation;

internal class ChunkGenerator(BlockRegistry blockRegistry)
{
    private readonly ConcurrentDictionary<Vec2<int>, int> maxHeightCache = [];
    
    public Chunk GenerateChunk(Vec3<int> index)
    {
        Chunk chunk = new(index, blockRegistry);
        Block[,,] buffer = Chunk.GetBuffer();

        var block = blockRegistry.GetNumericId("sandstone");

        int chunkWorldX = index.X * Chunk.Size;
        int chunkWorldY = index.Y * Chunk.Size;
        int chunkWorldZ = index.Z * Chunk.Size;
        
        const int terrainLevel = Chunk.Size / 2;

        for (int x = 0; x < Chunk.Size; x++)
        for (int z = 0; z < Chunk.Size; z++)
        {
            int worldX = chunkWorldX + x;
            int worldZ = chunkWorldZ + z;

            int terrainY = terrainLevel + GetTerrainHeight(worldX, worldZ);
            int localY = terrainY - chunkWorldY;

            if (localY < 0 || localY >= Chunk.Size)
            {
                continue;
            }

            buffer[x, localY, z] = new Block(block);
        }
        
        chunk.BuildPalette(buffer);
        
        return chunk;
    }
    
    public bool IsSunlight(Chunk chunk)
    {
        Vec2<int> column = new(chunk.Index.X, chunk.Index.Z);
        int maxElevation = GetMaximumColumnElevation(column);
        int surfaceChunkY = Chunk.WorldToChunkIndex(maxElevation);

        return chunk.Index.Y == surfaceChunkY ||
               chunk.Index.Y == surfaceChunkY + 1;
    }
    
    private const int TerrainLevel = Chunk.Size / 2;
    
    private int GetMaximumColumnElevation(Vec2<int> chunkIndex)
    {
        return maxHeightCache.GetOrAdd(chunkIndex, static index =>
        {
            int worldStartX = index.X * Chunk.Size;
            int worldStartZ = index.Z * Chunk.Size;
            int maximum = int.MinValue;

            for (int x = 0; x < Chunk.Size; x++)
            for (int z = 0; z < Chunk.Size; z++)
            {
                int worldX = worldStartX + x;
                int worldZ = worldStartZ + z;

                int elevation = TerrainLevel + GetTerrainHeight(worldX, worldZ);
                maximum = Math.Max(maximum, elevation);
            }

            return maximum;
        });
    }

    private static int GetTerrainHeight(int x, int z)
    {
        const float amplitude = 5.0f;
        const float frequency = 0.12f;

        float waveA = MathF.Sin(x * frequency);
        float waveB = MathF.Sin(z * frequency);
        float waveC = MathF.Sin((x + z) * frequency * 0.65f);

        float height =
            waveA * amplitude +
            waveB * amplitude * 0.75f +
            waveC * amplitude * 0.5f;

        return (int)MathF.Round(height);
    }
}
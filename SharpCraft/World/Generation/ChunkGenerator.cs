using SharpCraft.World.Chunks;
using SharpCraft.World.Blocks;
using SharpCraft.SharpMath;

namespace SharpCraft.World.Generation;

internal class ChunkGenerator(BlockRegistry blockRegistry)
{
    public Chunk GenerateChunk(Vec3<int> index)
    {
        Chunk chunk = new(index, blockRegistry);
        Block[,,] buffer = Chunk.GetBuffer();

        const uint block = 3;

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
using SharpCraft.Diagnostics;

namespace SharpCraft.World.Generation;

internal sealed class BenchmarkTerrain(BenchmarkScene scene, int seed)
{
    public bool IsSolid(int x, int y, int z)
    {
        if (scene == BenchmarkScene.Exposure)
            return y is >= 0 and < 32 && ((x ^ y ^ z ^ seed) & 1) == 0;
        
        if (scene == BenchmarkScene.Merge)
        {
            // Flat plateau, repeating walls, straight tunnels through its base
            if (y < 0 || y > SurfaceHeight(x, z)) return false;
            return !(y is >= 4 and <= 10 && Mod(z + seed, 64) is >= 12 and <= 19);
        }
        
        if (y < -32 || y > SurfaceHeight(x, z)) return false;
        // Smooth seeded hills and connected caves, rather than random voxel noise
        double cave = Math.Sin(x * .09 + (seed & 255)) * Math.Sin(z * .08) + Math.Cos(y * .22);
        return y > 4 || cave < 1.45;
    }

    public int SurfaceHeight(int x, int z) => scene switch
    {
        BenchmarkScene.Exposure => 31,
        BenchmarkScene.Merge => Mod(x + seed, 64) < 2 || Mod(z + seed, 64) < 2 ? 31 : 15,
        _ => 12 + (int)Math.Round(Noise(x, z, 64) * 20 + Noise(x, z, 24) * 6)
    };

    private double Noise(int x, int z, int scale)
    {
        int cellX = (int)Math.Floor((double)x / scale), cellZ = (int)Math.Floor((double)z / scale);
        double u = Mod(x, scale) / (double)scale, v = Mod(z, scale) / (double)scale;
        u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
        double a = Hash(cellX, cellZ), b = Hash(cellX + 1, cellZ);
        double c = Hash(cellX, cellZ + 1), d = Hash(cellX + 1, cellZ + 1);
        return (a + (b - a) * u) * (1 - v) + (c + (d - c) * u) * v;
    }

    private double Hash(int x, int z)
    {
        uint hash = unchecked((uint)x * 0x8da6b343u ^ (uint)z * 0xd8163841u ^ (uint)seed);
        hash ^= hash >> 16; hash *= 0x7feb352du; hash ^= hash >> 15; hash *= 0x846ca68bu; hash ^= hash >> 16;
        return hash / (double)uint.MaxValue * 2 - 1;
    }

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
}

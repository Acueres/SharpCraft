
namespace SharpCraft.World.Generation;

public class BiomeMapGenerator(int seed)
{
    readonly FastNoiseLite temperature = FastNoiseLite.GetNoise(seed + 5, 0.0020f, FastNoiseLite.FractalType.FBm, 3);
    readonly FastNoiseLite humidity = FastNoiseLite.GetNoise(seed + 6, 0.0020f, FastNoiseLite.FractalType.FBm, 3);

}

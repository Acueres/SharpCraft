using Microsoft.Xna.Framework;
using SharpCraft.MathUtilities;
using SharpCraft.World.Chunks;

namespace SharpCraft.Tests;

public class CoordinateConversionTests
{
    [Fact]
    public void TestChunkCoordinates()
    {
        Vector3 worldCoord = new(1.6f, 66.4f, -921.37f);
        worldCoord *= Chunk.Size;

        Vector3I index = Chunk.WorldToChunkCoords(worldCoord);

        Assert.Equal(new Vector3I(1, 66, -922), index);
    }
}

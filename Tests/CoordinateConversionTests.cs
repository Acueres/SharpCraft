using Microsoft.Xna.Framework;
using SharpCraft.MathUtilities;
using SharpCraft.World.Chunks;

namespace SharpCraft.Tests;

public class CoordinateConversionTests
{
    [Fact]
    public void TestChunkCoordinates()
    {
        // Mixed boundary, positive and negative coordinates test
        Vector3 worldPos = new(1f, 66.4f, -921.37f);
        worldPos *= Chunk.Size;

        Vector3I expected = new(1, 66, -922);
        Vector3I result = Chunk.WorldToChunkCoords(worldPos);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestWorldToBlockCoordinates()
    {
        // Positive coordinates test
        Vector3 worldPos = new(12.5f, 13.2f, 1.8f);
        Vector3I expected = new(12, 13, 1);
        Vector3I result = Chunk.WorldToBlockCoords(worldPos);
        Assert.Equal(expected, result);

        // Negative coordinates test
        Vector3 worldPosNeg = new(-0.2f, -1.2f, -0.5f);
        Vector3I expectedNeg = new(15, 14, 15);
        Vector3I resultNeg = Chunk.WorldToBlockCoords(worldPosNeg);
        Assert.Equal(expectedNeg, resultNeg);

        // Boundary test
        Vector3 worldPosBoundary = new(16f, 16f, 16f);
        Vector3I expectedBoundary = new(0, 0, 0);
        Vector3I resultBoundary = Chunk.WorldToBlockCoords(worldPosBoundary);
        Assert.Equal(expectedBoundary, resultBoundary);
    }
}

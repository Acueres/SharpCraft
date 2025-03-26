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

        Vec3<int> expected = new(1, 66, -922);
        Vec3<int> result = Chunk.WorldToChunkCoords(worldPos);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestWorldToBlockCoordinates()
    {
        // Positive coordinates test
        Vector3 worldPos = new(12.5f, 13.2f, 1.8f);
        Vec3<byte> expected = new(12, 13, 1);
        Vec3<byte> result = Chunk.WorldToBlockCoords(worldPos);
        Assert.Equal(expected, result);

        // Negative coordinates test
        Vector3 worldPosNeg = new(-0.2f, -1.2f, -0.5f);
        Vec3<byte> expectedNeg = new(15, 14, 15);
        Vec3<byte> resultNeg = Chunk.WorldToBlockCoords(worldPosNeg);
        Assert.Equal(expectedNeg, resultNeg);

        // Boundary test
        Vector3 worldPosBoundary = new(16f, 16f, 16f);
        Vec3<byte> expectedBoundary = new(0, 0, 0);
        Vec3<byte> resultBoundary = Chunk.WorldToBlockCoords(worldPosBoundary);
        Assert.Equal(expectedBoundary, resultBoundary);
    }
}

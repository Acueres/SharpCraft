using SharpCraft.SharpMath;
using SharpCraft.World.Chunks;
using SharpCraft.World.WorldStreaming;

namespace Tests;

public class ChunkVolumeTests
{
    private const int Radius = 2;

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(-1, 1, 1)]
    public void StartupScansTheFullVolumeAndExcludesLoadedChunks(int x, int y, int z)
    {
        var center = new Vec3<int>(x, y, z);
        var volume = new ChunkVolume(Radius);
        volume.InitCenter(center);
        var expected = Cube(center);

        AssertCoordinates(expected, volume.CollectIndexesForGeneration());
        Assert.Empty(volume.CollectIndexesForRemoval());

        // Volume collection only uses the chunk index, so no block assets are needed
        volume.Add(new Chunk(center, null!));
        expected.Remove(center);

        AssertCoordinates(expected, volume.CollectIndexesForGeneration());
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(-1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 0, -1)]
    [InlineData(1, -1, 0)]
    [InlineData(-1, 1, -1)]
    public void OneChunkMovesReturnEnteringAndLeavingCoordinates(int x, int y, int z)
    {
        var previousCenter = new Vec3<int>(10, -10, 5);
        var center = previousCenter + new Vec3<int>(x, y, z);
        var volume = new ChunkVolume(Radius);
        volume.InitCenter(previousCenter);
        volume.SetCenter(center);

        var previous = Cube(previousCenter);
        var current = Cube(center);

        AssertCoordinates(current.Except(previous), volume.CollectIndexesForGeneration());
        AssertCoordinates(previous.Except(current), volume.CollectIndexesForRemoval());
    }

    [Theory]
    [InlineData(2, 0, 0)]
    [InlineData(10, -10, 4)]
    public void TeleportsFindAllMissingCoordinatesAndRemoveTheOldVolume(int x, int y, int z)
    {
        var previousCenter = new Vec3<int>(10, -10, 5);
        var center = previousCenter + new Vec3<int>(x, y, z);
        var volume = new ChunkVolume(Radius);
        volume.InitCenter(previousCenter);
        var previous = Cube(previousCenter);
        foreach (var index in previous)
        {
            volume.Add(new Chunk(index, null!));
        }

        volume.SetCenter(center);
        var current = Cube(center);

        AssertCoordinates(current.Except(previous), volume.CollectIndexesForGeneration());
        AssertCoordinates(previous.Except(current), volume.CollectIndexesForRemoval());
    }

    [Fact]
    public void ConsecutiveMovesAndReversalsWorkBeforeGenerationFinishes()
    {
        var volume = new ChunkVolume(Radius);
        var previousCenter = new Vec3<int>(10, -10, 5);
        volume.InitCenter(previousCenter);

        Vec3<int>[] moves = [new(1, 0, 0), new(1, 1, 0), new(-1, 0, 0), new(-1, -1, 0)];
        foreach (var move in moves)
        {
            var center = previousCenter + move;
            volume.SetCenter(center);
            var previous = Cube(previousCenter);
            var current = Cube(center);

            AssertCoordinates(current.Except(previous), volume.CollectIndexesForGeneration());
            AssertCoordinates(previous.Except(current), volume.CollectIndexesForRemoval());
            previousCenter = center;
        }
    }

    private static HashSet<Vec3<int>> Cube(Vec3<int> center)
    {
        HashSet<Vec3<int>> result = [];
        for (int x = -Radius; x <= Radius; x++)
        for (int y = -Radius; y <= Radius; y++)
        for (int z = -Radius; z <= Radius; z++)
        {
            result.Add(center + new Vec3<int>(x, y, z));
        }

        return result;
    }

    private static void AssertCoordinates(IEnumerable<Vec3<int>> expected, List<Vec3<int>> actual)
    {
        var expectedSet = expected.ToHashSet();
        Assert.Equal(expectedSet.Count, actual.Count);
        Assert.True(expectedSet.SetEquals(actual));
    }
}

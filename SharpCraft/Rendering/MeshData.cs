using SharpCraft.Time;

namespace SharpCraft.Rendering;

internal class MeshData
{
    public BlockFace[] Faces { get; private set; }
    public BlockFace[] TransparentFaces { get; private set; }

    private readonly int apothem;
    private readonly List<BlockFace> faces = [];
    private readonly List<BlockFace> transparentFaces = [];
    private readonly List<(int X, int Z)> spiralPositions = [];
    private readonly Random random = new();

    private int nextPositionIndex;
    private double nextGrowthTime;

    public MeshData(int apothem)
    {
        this.apothem = apothem;

        BuildSpiralPositions();
        
        AddBlock(0, 0);
        nextPositionIndex = 1;

        Faces = faces.ToArray();
        TransparentFaces = transparentFaces.ToArray();
        
        nextGrowthTime = 1.0;
    }

    public bool Update(FrameTime time)
    {
        if (nextPositionIndex >= spiralPositions.Count)
        {
            return false;
        }

        if (time.TotalSeconds < nextGrowthTime)
        {
            return false;
        }

        while (
            nextPositionIndex < spiralPositions.Count &&
            time.TotalSeconds >= nextGrowthTime)
        {
            (int x, int z) = spiralPositions[nextPositionIndex];

            AddBlock(x, z);

            nextPositionIndex++;
            nextGrowthTime += 1.0;
        }

        Faces = faces.ToArray();
        TransparentFaces = transparentFaces.ToArray();
        return true;
    }

    private void AddBlock(int x, int z)
    {
        uint layer = (uint)random.Next(0, 20);

        for (int face = 0; face < (int)FaceDirection.Count; face++)
        {
            var blockFace = new BlockFace(
                2 * x,
                0,
                2 * z,
                (uint)face,
                layer,
                PackLight(15, 0)
            );
            
            if (layer == 0)
            {
                transparentFaces.Add(blockFace);
            }
            else
            {
                faces.Add(blockFace);
            }
        }
    }

    private void BuildSpiralPositions()
    {
        int totalBlocks = (2 * apothem + 1) * (2 * apothem + 1);

        spiralPositions.Add((0, 0));

        if (totalBlocks == 1)
        {
            return;
        }

        int x = 0;
        int z = 0;

        int dx = 1;
        int dz = 0;

        int segmentLength = 1;

        while (spiralPositions.Count < totalBlocks)
        {
            for (int segment = 0; segment < 2; segment++)
            {
                for (int step = 0; step < segmentLength; step++)
                {
                    x += dx;
                    z += dz;

                    if (Math.Abs(x) <= apothem && Math.Abs(z) <= apothem)
                    {
                        spiralPositions.Add((x, z));

                        if (spiralPositions.Count >= totalBlocks)
                        {
                            return;
                        }
                    }
                }

                // Rotate direction 90 degrees counter-clockwise.
                int oldDx = dx;
                dx = -dz;
                dz = oldDx;
            }

            segmentLength++;
        }
    }

    private static uint PackLight(byte skylight, byte blockLight)
    {
        return
            ((uint)skylight & 0xF) |
            (((uint)blockLight & 0xF) << 4);
    }
}
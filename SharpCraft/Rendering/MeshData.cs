using SharpCraft.Time;
using SharpCraft.SharpMath;
using SharpCraft.World.Blocks;

using System.Numerics;

namespace SharpCraft.Rendering;

internal class MeshData
{
    private const uint TerrainTextureLayer = 1;
    private const byte Skylight = 15;
    private const byte BlockLight = 0;

    public VoxelFace[] Faces { get; private set; } = [];
    public VoxelFace[] TransparentFaces { get; private set; } = [];

    private readonly TerrainBlock[] blocks;
    private bool needsInitialBuild = true;

    public MeshData(int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                "Terrain size must be positive."
            );
        }

        List<TerrainBlock> generatedBlocks = [];

        int start = -(size / 2);
        int end = start + size;

        for (int x = start; x < end; x++)
        {
            for (int z = start; z < end; z++)
            {
                int y = GetTerrainHeight(x, z);

                Vector3 center = new(
                    2 * x,
                    2 * y,
                    2 * z
                );

                generatedBlocks.Add(new TerrainBlock(
                    x,
                    y,
                    z,
                    new CubeBound(center, 1.0f)
                ));
            }
        }

        blocks = generatedBlocks.ToArray();
        BuildAllFaces();
    }

    public bool Update(in FrameTime time, Camera camera)
    {
        if (!needsInitialBuild && !camera.UpdateOccurred)
        {
            return false;
        }

        needsInitialBuild = false;

        RebuildVisibleFaces(camera.Frustum);

        return true;
    }

    private void RebuildVisibleFaces(in Frustum frustum)
    {
        List<VoxelFace> faces = [];

        foreach (TerrainBlock block in blocks)
        {
            if (!frustum.Intersects(block.Bound))
            {
                continue;
            }

            AddBlock(faces, block.X, block.Y, block.Z);
        }

        Faces = faces.ToArray();
        TransparentFaces = [];
    }

    private void BuildAllFaces()
    {
        List<VoxelFace> faces = [];

        foreach (TerrainBlock block in blocks)
        {
            AddBlock(faces, block.X, block.Y, block.Z);
        }

        Faces = faces.ToArray();
        TransparentFaces = [];
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

    private static void AddBlock(List<VoxelFace> faces, int x, int y, int z)
    {
        for (int face = 0; face < (int)FaceDirection.Count; face++)
        {
            faces.Add(new VoxelFace(
                2 * x,
                2 * y,
                2 * z,
                (uint)face,
                TerrainTextureLayer,
                PackLight(Skylight, BlockLight)
            ));
        }
    }

    private static uint PackLight(byte skylight, byte blockLight)
    {
        return
            ((uint)skylight & 0xF) |
            (((uint)blockLight & 0xF) << 4);
    }

    private readonly record struct TerrainBlock(
        int X,
        int Y,
        int Z,
        CubeBound Bound
    );
}
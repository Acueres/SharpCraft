namespace SharpCraft;

internal static class Cube
{
    public static readonly Vertex[] Vertices =
    [
        new(-1, -1, -1),
        new( 1, -1, -1),
        new( 1,  1, -1),
        new(-1,  1, -1),

        new(-1, -1,  1),
        new( 1, -1,  1),
        new( 1,  1,  1),
        new(-1,  1,  1),
    ];

    public static readonly ushort[] Indices =
    [
        // Front
        4, 5, 6,
        4, 6, 7,

        // Back
        1, 0, 3,
        1, 3, 2,

        // Left
        0, 4, 7,
        0, 7, 3,

        // Right
        5, 1, 2,
        5, 2, 6,

        // Top
        3, 7, 6,
        3, 6, 2,

        // Bottom
        0, 1, 5,
        0, 5, 4,
    ];
}

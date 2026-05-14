namespace SharpCraft.Rendering;

internal static class Cube
{
    public static readonly Vertex[] Vertices =
[
    // Front face (+Z)
    new(-1, -1,  1, 0, 1),
    new( 1, -1,  1, 1, 1),
    new( 1,  1,  1, 1, 0),
    new(-1,  1,  1, 0, 0),

    // Back face (-Z)
    new( 1, -1, -1, 0, 1),
    new(-1, -1, -1, 1, 1),
    new(-1,  1, -1, 1, 0),
    new( 1,  1, -1, 0, 0),

    // Left face (-X)
    new(-1, -1, -1, 0, 1),
    new(-1, -1,  1, 1, 1),
    new(-1,  1,  1, 1, 0),
    new(-1,  1, -1, 0, 0),

    // Right face (+X)
    new( 1, -1,  1, 0, 1),
    new( 1, -1, -1, 1, 1),
    new( 1,  1, -1, 1, 0),
    new( 1,  1,  1, 0, 0),

    // Top face (+Y)
    new(-1,  1,  1, 0, 1),
    new( 1,  1,  1, 1, 1),
    new( 1,  1, -1, 1, 0),
    new(-1,  1, -1, 0, 0),

    // Bottom face (-Y)
    new(-1, -1, -1, 0, 1),
    new( 1, -1, -1, 1, 1),
    new( 1, -1,  1, 1, 0),
    new(-1, -1,  1, 0, 0),
];

    public static readonly ushort[] Indices =
[
     0,  1,  2,  0,  2,  3,   // front
     4,  5,  6,  4,  6,  7,   // back
     8,  9, 10,  8, 10, 11,   // left
    12, 13, 14, 12, 14, 15,   // right
    16, 17, 18, 16, 18, 19,   // top
    20, 21, 22, 20, 22, 23,   // bottom
];
}

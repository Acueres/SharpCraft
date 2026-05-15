namespace SharpCraft.Rendering;

internal class MeshData
{
    public Vertex[] Vertices { get; }
    public uint[] Indices { get; }

    public MeshData(int size)
    {
        List<Vertex> verts = [];
        List<uint> inds = [];

        for (int x = -size; x < size; x++)
        {
            for (int z = -size; z < size; z++)
            {
                uint vertexOffset = (uint)verts.Count;

                foreach (var vertex in Cube.Vertices)
                {
                    float xComp = vertex.Position.X + 2 * x;
                    float zComp = vertex.Position.Z + 2 * z;

                    verts.Add(new Vertex(
                        xComp,
                        vertex.Position.Y,
                        zComp,
                        vertex.TexCoord.X,
                        vertex.TexCoord.Y
                    ));
                }

                foreach (var index in Cube.Indices)
                {
                    inds.Add(vertexOffset + index);
                }
            }
        }

        Vertices = verts.ToArray();
        Indices = inds.ToArray();
    }
}

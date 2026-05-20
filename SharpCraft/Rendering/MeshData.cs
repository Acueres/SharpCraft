namespace SharpCraft.Rendering;

internal class MeshData
{
    public BlockFace[] Faces { get; }

    public MeshData(int size)
    {
        List<BlockFace> faces = [];
        
        Random random = new Random();
        
        for (int x = -size; x < size; x++)
        {
            for (int z = -size; z < size; z++)
            {
                uint layer = (uint)random.Next(0, 20);

                for (int face = 0; face < (int)FaceDirection.Count; face++)
                {
                    faces.Add(new BlockFace(
                        2 * x,
                        0,
                        2 * z,
                        (uint)face,
                        layer,
                        PackLight(15, 0)
                    ));
                }
            }
        }
        
        Faces = faces.ToArray();
    }
    
    private static uint PackLight(byte skylight, byte blockLight)
    {
        return
            ((uint)skylight & 0xF) |
            (((uint)blockLight & 0xF) << 4);
    }
}

using SharpCraft.World.Blocks;

namespace SharpCraft.World.Chunks;

public class ChunkBuffer(Block[,,] blocks)
{
    readonly Block[,,] buffer = blocks;

    public Block this[int x, int y, int z]
    {
        get => buffer[x, y, z];
        set => buffer[x, y, z] = value;
    }
}

using SharpCraft.World.Chunks;

namespace SharpCraft.World.WorldStreaming;

internal class ChunkRecord
{
    public ulong Version;
    public Chunk? Chunk;
    public ChunkStage Stage;
    public ChunkFlags Flags;
}
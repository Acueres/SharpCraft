using SharpCraft.World.Chunks;

namespace SharpCraft.World.WorldStreaming;

internal class ChunkRecord
{
    public int Version;
    public Chunk? Chunk;
    public ChunkStage Stage;
    public ChunkFlags Flags;
}
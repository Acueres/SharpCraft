using SharpCraft.World.Chunks;

namespace SharpCraft.World.WorldStreaming;

[Flags]
internal enum ChunkDirty
{
    None = 0,
    NeedsRelight = 1,
    NeedsRemesh = 2
}

internal class ChunkRecord
{
    public ulong Version { get; set; }
    public Chunk? Chunk { get; set; }
    public ChunkStage Stage { get; set; }
    public ChunkFlags Flags { get; set; }
    public JobType? InFlight { get; set; }
    public ChunkDirty Dirty { get; set; }
}
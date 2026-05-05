using System;

using SharpCraft.World.Chunks;

namespace SharpCraft.World.ChunkStreaming;

enum ChunkStage
{
    Fresh,
    Generating,
    Generated,
    Linking,
    Lighting,
    Lit,
    Meshing,
    Meshed
}

[Flags]
enum ChunkFlags
{
    None = 0,
    Wanted = 1 << 0,
    DeleteRequested = 1 << 1
}

class ChunkRecord
{
    public int Version;
    public Chunk Chunk;
    public ChunkStage Stage;
    public ChunkFlags Flags;
}

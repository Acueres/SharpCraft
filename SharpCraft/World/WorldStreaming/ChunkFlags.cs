namespace SharpCraft.World.WorldStreaming;

[Flags]
internal enum ChunkFlags
{
    None = 0,
    Wanted = 1 << 0,
    DeleteRequested = 1 << 1
}
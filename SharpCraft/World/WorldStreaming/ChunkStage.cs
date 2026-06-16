namespace SharpCraft.World.WorldStreaming;

internal enum ChunkStage
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
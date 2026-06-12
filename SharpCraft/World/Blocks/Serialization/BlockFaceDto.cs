namespace SharpCraft.World.Blocks.Serialization;

internal sealed record BlockFaceDto(
    string Type,
    string Front,
    string Back,
    string Top,
    string Bottom,
    string Right,
    string Left);
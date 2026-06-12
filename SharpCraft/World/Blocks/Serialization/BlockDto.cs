namespace SharpCraft.World.Blocks.Serialization;

internal sealed record BlockDto(
    string? Name,
    string Type,
    bool Transparent,
    int LightLevel);
namespace SharpCraft.World.Blocks.Serialization;

internal sealed class BlockDto
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Texture { get; init; }
    public BlockTexturesDto? Textures { get; init; }
    public bool Transparent { get; init; }
    public int LightLevel { get; init; }
}
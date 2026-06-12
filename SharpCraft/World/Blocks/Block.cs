namespace SharpCraft.World.Blocks;

internal readonly record struct Block(uint Value)
{
    public const uint EmptyValue = 0;
    public static readonly Block Empty = new(EmptyValue);
    public bool IsEmpty => Value == EmptyValue;

    public override string ToString() => IsEmpty ? "Empty" : $"Block({Value})";
}
namespace SharpCraft.World.Blocks;

public readonly struct Block(ushort value)
{
    public readonly ushort Value { get; } = value;
    public static Block Empty => new(EmptyValue);
    public bool IsEmpty => Value == EmptyValue;

    public const ushort EmptyValue = default;

    public static bool operator ==(Block a, Block b)
    {
        return a.Value == b.Value;
    }

    public static bool operator !=(Block a, Block b)
    {
        return a.Value != b.Value;
    }

    public override bool Equals(object obj)
    {
        return ((Block)obj).Value == Value;
    }

    public override int GetHashCode()
    {
        const int prime = 397;
        return Value * prime;
    }
}

namespace SharpCraft.World.Blocks
{
    public readonly struct Block(ushort value)
    {
        public readonly ushort Value { get; init; } = value;
        public static Block Empty => new(EmptyValue);
        public bool IsEmpty => Value == EmptyValue;

        public const ushort EmptyValue = default;
    }
}

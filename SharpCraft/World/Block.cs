namespace SharpCraft.World
{
    public readonly struct Block
    {
        public readonly ushort? Value { get; init; }
        public bool IsEmpty => Value is null;

        public Block()
        {
            Value = null;
        }

        public Block(ushort value)
        {
            Value = value;
        }

        public Block(ushort? value)
        {
            Value = value;
        }
    }
}

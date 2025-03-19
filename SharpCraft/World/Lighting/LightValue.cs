namespace SharpCraft.World.Lighting
{
    public readonly struct LightValue
    {
        readonly byte value = 0;

        public byte Value => value;
        public byte SkyValue => (byte)(value >> 4 & 0xF);
        public byte BlockValue => (byte)(value >> 0 & 0xF);

        public static LightValue Null => new(0, 0);
        public static LightValue Sunlight => new(MaxValue, 0);
        public const byte MaxValue = 15;

        public LightValue(byte sky, byte block)
        {
            value = (byte)(value & 0xF | sky << 4);
            value = (byte)(value & 0xF0 | block);
        }

        public bool Compare(LightValue other, out LightValue result)
        {
            byte skyValue = SkyValue;
            bool skyCondition = SkyValue < other.SkyValue;
            if (skyCondition)
            {
                skyValue = other.SkyValue;
            }

            byte blockValue = BlockValue;
            bool blockCondition = BlockValue < other.BlockValue;
            if (blockCondition)
            {
                blockValue = other.BlockValue;
            }

            result = new(skyValue, blockValue);

            return skyCondition || blockCondition;
        }

        public LightValue SubtractSkyValue(byte amount)
        {
            byte skyValue = (byte)(SkyValue - amount);
            return new LightValue(skyValue, BlockValue);
        }

        public LightValue SubtractBlockValue(byte amount)
        {
            if (BlockValue == 0) return this;

            byte blockValue = (byte)(BlockValue - amount);
            return new LightValue(SkyValue, blockValue);
        }

        public static bool operator ==(LightValue a, LightValue b)
        => a.SkyValue == b.SkyValue && a.BlockValue == b.BlockValue;

        public static bool operator !=(LightValue a, LightValue b)
        => a.SkyValue != b.SkyValue && a.BlockValue != b.BlockValue;

        public static bool operator >(LightValue a, LightValue b)
        => a.SkyValue > b.SkyValue || a.BlockValue > b.BlockValue;

        public static bool operator <(LightValue a, LightValue b)
        => a.SkyValue < b.SkyValue || a.BlockValue < b.BlockValue;
    }
}

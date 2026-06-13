namespace SharpCraft.World.Chunks;

internal class BitStorage
{
    private readonly int size;
    private readonly int size2;
    private readonly int bitsPerEntry;
    private readonly ulong[] data;

    public BitStorage(int size, int bitsPerEntry)
    {
        this.size = size;
        size2 = size * size;
        this.bitsPerEntry = bitsPerEntry;

        int totalVoxels = size * size * size;
        // total bits needed
        long totalBits = (long)totalVoxels * bitsPerEntry;
        if (totalBits % 64 != 0)
        {
            throw new ArgumentException(
                $"BitStorage requires size³ × bitsPerEntry to be a multiple of 64 " +
                $"(got {totalVoxels} voxels × {bitsPerEntry} bits = {totalBits}).");
        }

        long arrayLength = totalBits / 64;
        data = new ulong[arrayLength];
    }

    public uint this[int x, int y, int z]
    {
        get => Get(x, y, z);
        set => Set(x, y, z, value);
    }

    public void Set(int x, int y, int z, uint value)
    {
        int index = ToLinearIndex(x, y, z);
        long bitPos = (long)index * bitsPerEntry;
        int arrIndex = (int)(bitPos / 64);
        int bitOffset = (int)(bitPos % 64);

        // mask for the bits we want to store
        ulong mask = (1UL << bitsPerEntry) - 1UL;

        // clear existing bits at the correct position in data[arrIndex]
        data[arrIndex] &= ~(mask << bitOffset);
        // insert new bits 
        data[arrIndex] |= (value & mask) << bitOffset;

        // if it crosses boundary of this 64-bit block
        int bitsInFirstLong = 64 - bitOffset;
        if (bitsInFirstLong < bitsPerEntry)
        {
            int bitsOverflow = bitsPerEntry - bitsInFirstLong;
            // clear those overflow bits in data[arrIndex + 1]
            data[arrIndex + 1] &= ~((1UL << bitsOverflow) - 1UL);
            // insert the overflow
            data[arrIndex + 1] |= ((ulong)value >> bitsInFirstLong) & ((1UL << bitsOverflow) - 1UL);
        }
    }

    /// <summary>
    /// Retrieves the stored value (fitting in bitsPerEntry bits) at [x,y,z].
    /// </summary>
    public uint Get(int x, int y, int z)
    {
        int index = ToLinearIndex(x, y, z);
        long bitPos = (long)index * bitsPerEntry;
        int arrIndex = (int)(bitPos / 64);
        int bitOffset = (int)(bitPos % 64);

        ulong mask = (1UL << bitsPerEntry) - 1UL;

        // shift down so the bits at bitOffset are at the bottom
        ulong result = data[arrIndex] >> bitOffset;

        int bitsInFirstLong = 64 - bitOffset;
        // if the boundary is crossed, fetch the remainder from data[arrIndex + 1]
        if (bitsInFirstLong < bitsPerEntry)
        {
            int bitsOverflow = bitsPerEntry - bitsInFirstLong;
            ulong overflow = data[arrIndex + 1] & ((1UL << bitsOverflow) - 1UL);
            result |= overflow << bitsInFirstLong;
        }

        return (uint)(result & mask);
    }

    private int ToLinearIndex(int x, int y, int z)
    {
        return x + y * size + z * size2;
    }
}
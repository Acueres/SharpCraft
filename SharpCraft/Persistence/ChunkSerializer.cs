using System;
using System.Buffers;
using System.IO;
using System.Text;

using SharpCraft.MathUtilities;
using SharpCraft.World.Chunks;

namespace SharpCraft.Persistence;

public class ChunkSerializer(Chunk chunk)
{
    public Vec3<int> Index { get; } = chunk.Index;
    
    /// <summary>
    /// Serializes the chunk into a binary blob:
    /// - header (magic/index)
    /// - palette of global block IDs
    /// - bit-packed indices
    /// </summary>
    public byte[] SerializeChunk()
    {
        if (chunk.IsEmpty)
        {
            return [];
        }

        int paletteCount = chunk.PaletteCount;
        int bitsPerBlock = Chunk.GetBitsPerBlock(paletteCount);
        
        const int blockCount = Chunk.Size * Chunk.Size * Chunk.Size;
        uint[] indices = ArrayPool<uint>.Shared.Rent(blockCount);
        try
        {
            int i = 0;
            for (int y = 0; y < Chunk.Size; y++)
            for (int x = 0; x < Chunk.Size; x++)
            for (int z = 0; z < Chunk.Size; z++)
                indices[i++] = chunk.GetStorageValue(x, y, z);

            byte[] packed = PackBitIndices(indices, blockCount, bitsPerBlock);

            // Build output
            using var ms = new MemoryStream(64 + paletteCount * 2 + packed.Length);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            // Header
            bw.Write("SCNK"u8.ToArray());
            bw.Write(chunk.Index.X);
            bw.Write(chunk.Index.Y);
            bw.Write(chunk.Index.Z);

            // Palette
            bw.Write((ushort)paletteCount);
            for (int p = 0; p < paletteCount; p++)
                bw.Write(chunk.GetPaletteValue(p)); // global id

            // Packed indices
            bw.Write((byte)bitsPerBlock);
            bw.Write(packed.Length);
            bw.Write(packed);

            bw.Flush();
            return ms.ToArray();
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(indices, clearArray: false);
        }
    }

    // Packs N indices (each bitsPerBlock wide) into a byte[] (little-endian bit stream)
    static byte[] PackBitIndices(uint[] indices, int count, int bitsPerBlock)
    {
        if (bitsPerBlock <= 0) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bitsPerBlock);
        if (count < 0) ArgumentOutOfRangeException.ThrowIfNegative(count);

        int totalBits = count * bitsPerBlock;
        int byteLen = (totalBits + 7) / 8;
        byte[] output = new byte[byteLen];

        ulong acc = 0;
        int accBits = 0;
        int outPos = 0;

        uint mask = bitsPerBlock == 32 ? 0xFFFFFFFFu : ((1u << bitsPerBlock) - 1u);

        for (int i = 0; i < count; i++)
        {
            uint v = indices[i] & mask;

            acc |= (ulong)v << accBits;
            accBits += bitsPerBlock;

            while (accBits >= 8)
            {
                output[outPos++] = (byte)(acc & 0xFF);
                acc >>= 8;
                accBits -= 8;
            }
        }

        if (accBits > 0 && outPos < output.Length)
            output[outPos] = (byte)(acc & 0xFF);

        return output;
    }
}
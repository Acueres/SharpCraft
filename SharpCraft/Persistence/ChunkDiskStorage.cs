using System;
using System.IO;
using System.Text;
using SharpCraft.MathUtilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;

namespace SharpCraft.Persistence;

public static class ChunkDiskStorage
{
    private static readonly byte[] Magic = "SCNK"u8.ToArray();

    // Example: Saves/<saveName>/chunks/x_y_z.scnk
    private static string GetChunkPath(string saveName, Vec3<int> chunkIndex)
    {
        return Path.Combine(
            Directory.GetCurrentDirectory(),
            "Saves",
            saveName,
            "chunks",
            $"{chunkIndex.X}_{chunkIndex.Y}_{chunkIndex.Z}.scnk"
        );
    }

    /// <summary>
    /// Writes bytes atomically (write temp + replace). If data is null, deletes the file.
    /// </summary>
    public static void WriteChunkAtomic(string saveName, Vec3<int> chunkIndex, byte[] data)
    {
        string path = GetChunkPath(saveName, chunkIndex);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (data.Length == 0)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        string tmp = path + ".tmp";

        // Write temp
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(data, 0, data.Length);
            fs.Flush(true);
        }

        // Atomic replace
        File.Move(tmp, path, overwrite: true);
    }

    public static bool TryLoadChunk(string saveName, Vec3<int> index, out Block[,,] buffer)
    {
        buffer = null;

        string path = GetChunkPath(saveName, index);
        if (!File.Exists(path))
            return false;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return TryDeserialize(fs, index, out buffer);
        }
        catch
        {
            buffer = null;
            return false;
        }
    }

    private static bool TryDeserialize(Stream stream, Vec3<int> expectedIndex, out Block[,,] buffer)
    {
        buffer = null;

        using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // Magic
        var magic = br.ReadBytes(4);
        if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] ||
            magic[3] != Magic[3])
            return false;

        // Index
        int x = br.ReadInt32();
        int y = br.ReadInt32();
        int z = br.ReadInt32();

        if (x != expectedIndex.X || y != expectedIndex.Y || z != expectedIndex.Z)
            return false;

        // Palette
        ushort paletteCount = br.ReadUInt16();
        if (paletteCount == 0)
            return false;

        ushort[] palette = new ushort[paletteCount];
        for (int i = 0; i < paletteCount; i++)
            palette[i] = br.ReadUInt16();

        // Packed indices
        byte bitsPerBlock = br.ReadByte();
        int packedLen = br.ReadInt32();
        if (bitsPerBlock == 0 || packedLen < 0)
            return false;

        byte[] packed = br.ReadBytes(packedLen);
        if (packed.Length != packedLen)
            return false;

        const int count = Chunk.Size * Chunk.Size * Chunk.Size;
        int expectedPackedLen = (count * bitsPerBlock + 7) / 8;
        if (packedLen != expectedPackedLen)
            return false;

        // Unpack -> palette indices -> buffer
        buffer = Chunk.GetBlockArray();

        Span<uint> indices = stackalloc uint[count];
        UnpackBitIndices(packed, indices, bitsPerBlock);

        int idx = 0;
        for (int yy = 0; yy < Chunk.Size; yy++)
        for (int xx = 0; xx < Chunk.Size; xx++)
        for (int zz = 0; zz < Chunk.Size; zz++)
        {
            uint pi = indices[idx++];
            if (pi >= paletteCount)
                return false;

            ushort globalId = palette[pi];
            if (globalId != Block.EmptyValue)
                buffer[xx, yy, zz] = new Block(globalId);
        }

        return true;
    }

    // Little-endian bitstream unpack
    private static void UnpackBitIndices(ReadOnlySpan<byte> packed, Span<uint> output, int bitsPerBlock)
    {
        uint mask = bitsPerBlock == 32 ? 0xFFFFFFFFu : ((1u << bitsPerBlock) - 1u);

        ulong acc = 0;
        int accBits = 0;
        int inPos = 0;

        for (int i = 0; i < output.Length; i++)
        {
            while (accBits < bitsPerBlock)
            {
                acc |= (ulong)packed[inPos++] << accBits;
                accBits += 8;
            }

            output[i] = (uint)(acc & mask);
            acc >>= bitsPerBlock;
            accBits -= bitsPerBlock;
        }
    }
}
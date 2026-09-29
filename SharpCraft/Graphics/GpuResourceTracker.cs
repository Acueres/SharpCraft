namespace SharpCraft.Graphics;

// Requested sizes of live engine-owned buffers and textures
internal readonly record struct GpuResourceUsage(long BufferBytes, long TextureBytes,
    int BufferCount, int TextureCount)
{
    public long TotalBytes => checked(BufferBytes + TextureBytes);
}

internal sealed class GpuResourceTracker
{
    private readonly Lock gate = new();
    private readonly Dictionary<nint, long> buffers = [];
    private readonly Dictionary<nint, long> textures = [];
    
    private long bufferBytes;
    private long textureBytes;

    public GpuResourceUsage Snapshot
    {
        get
        {
            lock (gate)
            {
                return new(bufferBytes, textureBytes, buffers.Count, textures.Count);
            }
        }
    }

    public void AddBuffer(nint handle, long bytes)
    {
        lock (gate)
        {
            Add(buffers, handle, bytes, ref bufferBytes);
        }
    }

    public void RemoveBuffer(nint handle)
    {
        lock (gate)
        {
            Remove(buffers, handle, ref bufferBytes);
        }
    }

    public void AddTexture(nint handle, long bytes)
    {
        lock (gate)
        {
            Add(textures, handle, bytes, ref textureBytes);
        }
    }

    public void RemoveTexture(nint handle)
    {
        lock (gate)
        {
            Remove(textures, handle, ref textureBytes);
        }
    }

    private static void Add(Dictionary<nint, long> entries, nint handle, long bytes, ref long total)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        long nextTotal = checked(total + bytes);
        entries.Add(handle, bytes);
        total = nextTotal;
    }

    private static void Remove(Dictionary<nint, long> entries, nint handle, ref long total)
    {
        if (!entries.Remove(handle, out long bytes))
        {
            throw new InvalidOperationException("GPU resource was not registered or was already released");
        }

        total -= bytes;
    }
}

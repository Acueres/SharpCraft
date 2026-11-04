using SDL;
using SharpCraft.Graphics;

namespace Tests;

internal sealed unsafe class FakeBufferDevice : IGpuBufferDevice
{
    internal readonly record struct BufferInfo(nint Handle, uint Bytes, SDL_GPUBufferUsageFlags Usage);

    private readonly Dictionary<nint, BufferInfo> live = [];
    private nint nextHandle = 1;
    private int creationAttempts;

    public int? FailOnCreation { get; init; }
    public List<BufferInfo> Created { get; } = [];
    public HashSet<nint> Released { get; } = [];
    public int LiveCount => live.Count;
    public long LiveBytes => live.Values.Sum(buffer => buffer.Bytes);

    public SDL_GPUBuffer* CreateBuffer(SDL_GPUBufferCreateInfo* info)
    {
        if (++creationAttempts == FailOnCreation)
            throw new InvalidOperationException("Injected buffer creation failure.");

        if (info == null || info->size == 0)
            throw new InvalidOperationException("Invalid buffer description.");

        var buffer = new BufferInfo(nextHandle++, info->size, info->usage);
        live.Add(buffer.Handle, buffer);
        Created.Add(buffer);
        return (SDL_GPUBuffer*)buffer.Handle;
    }

    public void ReleaseBuffer(SDL_GPUBuffer* buffer)
    {
        nint handle = (nint)buffer;
        if (!live.Remove(handle) || !Released.Add(handle))
            throw new InvalidOperationException("Buffer is foreign or already released.");
    }

    public bool IsLive(SDL_GPUBuffer* buffer) => live.ContainsKey((nint)buffer);
}

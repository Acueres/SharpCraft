using SDL;

using SharpCraft.Platform;
using SharpCraft.Rendering;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class IndexBuffer : IDisposable
{
    public SDL_GPUBuffer* Handle => buffer;
    public uint Count { get; }

    private readonly GpuDevice device;
    private readonly SDL_GPUBuffer* buffer;

    public IndexBuffer(uint count, GpuDevice device)
    {
        Count = count;
        this.device = device;

        SDL_GPUBufferCreateInfo indexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX,
            size = count * sizeof(uint)
        };

        buffer = SDL_CreateGPUBuffer(device.Handle, &indexBufferInfo);
        if (buffer == null)
        {
            SdlRuntime.Throw("Failed to create index buffer");
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        SDL_ReleaseGPUBuffer(device.Handle, buffer);

        disposed = true;
    }
}

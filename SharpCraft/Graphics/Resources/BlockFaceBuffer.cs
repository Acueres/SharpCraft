using SDL;

using SharpCraft.Platform;
using SharpCraft.Rendering;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class BlockFaceBuffer : IDisposable
{
    public SDL_GPUBuffer* Handle => buffer;
    public uint Count { get; }

    private readonly GpuDevice device;
    private readonly SDL_GPUBuffer* buffer;

    public BlockFaceBuffer(uint count, GpuDevice device)
    {
        Count = count;
        this.device = device;

        SDL_GPUBufferCreateInfo vertexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
            size = count * (uint)sizeof(BlockFace)
        };

        buffer = SDL_CreateGPUBuffer(device.Handle, &vertexBufferInfo);
        if (buffer == null)
        {
            SdlRuntime.Throw("Failed to create block face buffer");
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
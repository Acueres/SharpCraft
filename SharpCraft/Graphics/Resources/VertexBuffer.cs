using SDL;

using SharpCraft.Platform;
using SharpCraft.Rendering;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class VertexBuffer : IDisposable
{
    public SDL_GPUBuffer* Handle => buffer;

    private readonly GpuDevice device;
    private readonly SDL_GPUBuffer* buffer;

    public VertexBuffer(GpuDevice device)
    {
        this.device = device;

        SDL_GPUBufferCreateInfo vertexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
            size = (uint)(Cube.Vertices.Length * sizeof(Vertex))
        };

        buffer = SDL_CreateGPUBuffer(device.Handle, &vertexBufferInfo);
        if (buffer == null)
        {
            SdlRuntime.Throw("Failed to create vertex buffer");
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

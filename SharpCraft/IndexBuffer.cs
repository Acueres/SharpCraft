using SDL;

using static SDL.SDL3;

namespace SharpCraft;

internal unsafe class IndexBuffer : IDisposable
{
    public SDL_GPUBuffer* Handle => buffer;

    private readonly GpuDevice device;
    private readonly SDL_GPUBuffer* buffer;

    public IndexBuffer(GpuDevice device)
    {
        this.device = device;

        SDL_GPUBufferCreateInfo indexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX,
            size = (uint)(Cube.Indices.Length * sizeof(ushort))
        };

        buffer = SDL_CreateGPUBuffer(device.Handle, &indexBufferInfo);
        if (buffer == null)
        {
            SdlRuntime.Throw("Failed to create index buffer");
        }
    }

    public void Dispose()
    {
        SDL_ReleaseGPUBuffer(device.Handle, buffer);
    }
}

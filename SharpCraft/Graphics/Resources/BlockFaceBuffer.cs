using SDL;

using SharpCraft.Platform;
using SharpCraft.Rendering;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class BlockFaceBuffer : IDisposable
{
    public SDL_GPUBuffer* Handle => buffer;
    
    public uint BytesCount => (uint)(Count * sizeof(BlockFace));
    public uint Count { get; private set; }

    private uint capacity;

    private readonly GpuDevice device;
    private SDL_GPUBuffer* buffer;

    public BlockFaceBuffer(uint count, GpuDevice device)
    {
        Count = count;
        capacity = count;
        this.device = device;

        buffer = CreateBuffer(count, device);
    }

    public void EnsureSize(uint count)
    {
        if (count > capacity)
        {
            capacity = Math.Max(capacity, 1);
            
            while (capacity < count)
            {
                capacity *= 2;
            }
            
            var newBuffer = CreateBuffer(capacity, device);
            
            SDL_ReleaseGPUBuffer(device.Handle, buffer);
            
            buffer = newBuffer;
        }
        
        Count = count;
    }

    private static SDL_GPUBuffer* CreateBuffer(uint capacity, GpuDevice device)
    {
        SDL_GPUBufferCreateInfo vertexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
            size = capacity * (uint)sizeof(BlockFace)
        };

        var buffer = SDL_CreateGPUBuffer(device.Handle, &vertexBufferInfo);
        if (buffer == null)
        {
            SdlRuntime.Throw("Failed to create block face buffer");
        }

        return buffer;
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        SDL_ReleaseGPUBuffer(device.Handle, buffer);

        disposed = true;
    }
}
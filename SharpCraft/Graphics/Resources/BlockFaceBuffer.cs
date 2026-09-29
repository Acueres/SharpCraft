using SDL;

using SharpCraft.Platform;
using SharpCraft.Rendering;

namespace SharpCraft.Graphics.Resources;

internal unsafe class BlockFaceBuffer(GpuDevice device) : IDisposable
{
    public SDL_GPUBuffer* Handle => buffer;
    
    public uint BytesCount => (uint)(Count * sizeof(VoxelFace));
    public long CapacityBytes => capacity * sizeof(VoxelFace);
    public uint Count { get; private set; } = 1;

    private uint capacity = 1;

    private SDL_GPUBuffer* buffer = CreateBuffer(1, device);

    public void EnsureSize(uint count)
    {
        if (count > capacity)
        {
            uint newCapacity = Math.Max(capacity, 1);
            
            while (newCapacity < count)
            {
                newCapacity *= 2;
            }
            
            var newBuffer = CreateBuffer(newCapacity, device);
            
            device.ReleaseBuffer(buffer);
            
            buffer = newBuffer;
            capacity = newCapacity;
        }
        
        Count = count;
    }

    private static SDL_GPUBuffer* CreateBuffer(uint capacity, GpuDevice device)
    {
        SDL_GPUBufferCreateInfo vertexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
            size = capacity * (uint)sizeof(VoxelFace)
        };

        var buffer = device.CreateBuffer(&vertexBufferInfo);
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

        device.ReleaseBuffer(buffer);

        disposed = true;
    }
}

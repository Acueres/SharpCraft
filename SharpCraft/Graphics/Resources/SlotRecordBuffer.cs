using SDL;

using SharpCraft.Platform;
using SharpCraft.Rendering;

namespace SharpCraft.Graphics.Resources;

internal unsafe class SlotRecordBuffer(uint capacity, IGpuBufferDevice device) : IDisposable
{
    public SDL_GPUBuffer* Handle => buffer;
    
    public uint BytesCount => (uint)(Count * sizeof(SlotRecord));
    public long CapacityBytes => capacity * sizeof(SlotRecord);
    public uint Count { get; private set; }

    private uint capacity = capacity;

    private SDL_GPUBuffer* buffer = CreateBuffer(capacity, device);

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

    private static SDL_GPUBuffer* CreateBuffer(uint capacity, IGpuBufferDevice device)
    {
        if (capacity == 0)
        {
            throw new ArgumentException("Capacity must be greater than zero.");
        }
        
        SDL_GPUBufferCreateInfo vertexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ,
            size = checked(capacity * (uint)sizeof(SlotRecord)),
        };

        var buffer = device.CreateBuffer(&vertexBufferInfo);
        if (buffer == null)
        {
            SdlRuntime.Throw("Failed to create slot storage buffer");
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

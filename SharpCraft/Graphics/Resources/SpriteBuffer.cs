using SharpCraft.Platform;
using SharpCraft.Rendering;

using SDL;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal sealed unsafe class SpriteBuffer : IDisposable
{
    public SDL_GPUBuffer* VertexHandle => vertexBuffer;
    public SDL_GPUBuffer* IndexHandle => indexBuffer;

    public uint SpriteCount { get; private set; }
    public uint VertexCount => SpriteCount * 4;
    public uint IndexCount => SpriteCount * 6;

    public uint VertexBytesCount => VertexCount * (uint)sizeof(SpriteVertex);
    public uint IndexBytesCount => IndexCount * sizeof(uint);

    private readonly GpuDevice device;

    private SDL_GPUBuffer* vertexBuffer;
    private SDL_GPUBuffer* indexBuffer;

    private uint spriteCapacity;

    public SpriteBuffer(GpuDevice device, uint initialSpriteCapacity = 1)
    {
        this.device = device;

        spriteCapacity = Math.Max(initialSpriteCapacity, 1);

        vertexBuffer = CreateVertexBuffer(device, spriteCapacity * 4);
        indexBuffer = CreateIndexBuffer(device, spriteCapacity * 6);
    }

    public void EnsureSize(uint spriteCount)
    {
        if (spriteCount > spriteCapacity)
        {
            uint newCapacity = spriteCapacity;

            while (newCapacity < spriteCount)
            {
                newCapacity *= 2;
            }

            SDL_GPUBuffer* newVertexBuffer =
                CreateVertexBuffer(device, newCapacity * 4);

            SDL_GPUBuffer* newIndexBuffer =
                CreateIndexBuffer(device, newCapacity * 6);

            SDL_ReleaseGPUBuffer(device.Handle, vertexBuffer);
            SDL_ReleaseGPUBuffer(device.Handle, indexBuffer);

            vertexBuffer = newVertexBuffer;
            indexBuffer = newIndexBuffer;
            spriteCapacity = newCapacity;
        }

        SpriteCount = spriteCount;
    }

    private static SDL_GPUBuffer* CreateVertexBuffer(GpuDevice device, uint vertexCount)
    {
        SDL_GPUBufferCreateInfo info = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
            size = vertexCount * (uint)sizeof(SpriteVertex)
        };

        SDL_GPUBuffer* buffer = SDL_CreateGPUBuffer(device.Handle, &info);
        if (buffer == null)
        {
            SdlRuntime.Throw("Failed to create sprite vertex buffer");
        }

        return buffer;
    }

    private static SDL_GPUBuffer* CreateIndexBuffer(GpuDevice device, uint indexCount)
    {
        SDL_GPUBufferCreateInfo info = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX,
            size = indexCount * sizeof(uint)
        };

        SDL_GPUBuffer* buffer = SDL_CreateGPUBuffer(device.Handle, &info);
        if (buffer == null)
        {
            SdlRuntime.Throw("Failed to create sprite index buffer");
        }

        return buffer;
    }

    private bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        SDL_ReleaseGPUBuffer(device.Handle, vertexBuffer);
        SDL_ReleaseGPUBuffer(device.Handle, indexBuffer);

        disposed = true;
    }
}
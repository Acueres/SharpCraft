using SDL;

namespace SharpCraft.Graphics;

// Buffer ownership used by arenas
internal unsafe interface IGpuBufferDevice
{
    SDL_GPUBuffer* CreateBuffer(SDL_GPUBufferCreateInfo* info);
    void ReleaseBuffer(SDL_GPUBuffer* buffer);
}

using SDL;

namespace SharpCraft.Graphics;

internal readonly unsafe struct FrameContext(SDL_GPUCommandBuffer* commandBuffer,
    SDL_GPUTexture* swapchainTexture, uint width, uint height)
{
    public readonly SDL_GPUCommandBuffer* CommandBuffer { get; } = commandBuffer;
    public readonly SDL_GPUTexture* SwapchainTexture { get; } = swapchainTexture;
    public readonly uint Width { get; } = width;
    public readonly uint Height { get; } = height;
}
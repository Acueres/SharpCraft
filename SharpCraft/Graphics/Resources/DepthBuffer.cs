using SDL;

using SharpCraft.Platform;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class DepthBuffer : IDisposable
{
    public SDL_GPUTexture* Handle => depthTexture;

    private readonly GpuDevice device;
    private SDL_GPUTexture* depthTexture;

    private uint width;
    private uint height;

    public DepthBuffer(GpuDevice device, uint width, uint height)
    {
        this.device = device;
        this.width = width;
        this.height = height;

        depthTexture = CreateDepthTexture(width, height);
    }

    public void EnsureSize(uint newWidth, uint newHeight)
    {
        if (newWidth == 0 || newHeight == 0)
            return;

        if (depthTexture != null &&
            width == newWidth &&
            height == newHeight)
        {
            return;
        }

        SDL_GPUTexture* newDepthTexture = CreateDepthTexture(newWidth, newHeight);

        device.WaitIdle();

        if (depthTexture != null)
        {
            device.ReleaseTexture(depthTexture);
        }

        depthTexture = newDepthTexture;
        width = newWidth;
        height = newHeight;
    }

    private SDL_GPUTexture* CreateDepthTexture(uint width, uint height)
    {
        SDL_GPUTextureCreateInfo depthTextureInfo = new()
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D24_UNORM,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET,
            width = width,
            height = height,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1
        };

        SDL_GPUTexture* depthTexture = SDL_CreateGPUTexture(device.Handle, &depthTextureInfo);
        if (depthTexture == null)
        {
            SdlRuntime.Throw("Failed to create depth texture");
        }

        return depthTexture;
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        device.ReleaseTexture(depthTexture);
        disposed = true;
        depthTexture = null;
    }
}

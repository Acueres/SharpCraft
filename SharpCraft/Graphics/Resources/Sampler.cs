using SharpCraft.Platform;
using SDL;
using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal sealed unsafe class Sampler : IDisposable
{
    public SDL_GPUSampler* Handle => sampler;

    private readonly GpuDevice device;
    private readonly SDL_GPUSampler* sampler;

    public static Sampler CreateNearestRepeat(GpuDevice device)
    {
        SDL_GPUSamplerCreateInfo createInfo = new()
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,

            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT
        };

        return new Sampler(device, &createInfo);
    }

    public static Sampler CreateNearestClamp(GpuDevice device)
    {
        SDL_GPUSamplerCreateInfo createInfo = new()
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,

            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE
        };

        return new Sampler(device, &createInfo);
    }
    
    public static Sampler CreateLinearClamp(GpuDevice device)
    {
        SDL_GPUSamplerCreateInfo createInfo = new()
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,

            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE
        };

        return new Sampler(device, &createInfo);
    }

    private Sampler(GpuDevice device, SDL_GPUSamplerCreateInfo* createInfo)
    {
        this.device = device;

        sampler = SDL_CreateGPUSampler(device.Handle, createInfo);

        if (sampler == null)
        {
            SdlRuntime.Throw("Failed to create GPU sampler");
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed)
            return;

        SDL_ReleaseGPUSampler(device.Handle, sampler);
        disposed = true;
    }
}
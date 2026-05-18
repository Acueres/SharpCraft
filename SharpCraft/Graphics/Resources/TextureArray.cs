using SharpCraft.Platform;
using SDL;
using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class TextureArray : IDisposable
{
    public SDL_GPUTexture* Handle => texture;
    public byte[,] Data { get; }
    public uint LayerCount => (uint)Data.GetLength(0);

    public uint Width { get; }
    public uint Height { get; }

    private readonly GpuDevice device;
    private readonly SDL_GPUTexture* texture;

    public TextureArray(GpuDevice device, uint width, uint height, byte[,] data)
    {
        this.device = device;

        Width = width;
        Height = height;

        Data = data;

        SDL_GPUTextureCreateInfo createInfo = new()
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D_ARRAY,

            // RGBA8 pixel data from StbImageSharp
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,

            // Required if the texture will be sampled in shaders
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,

            width = width,
            height = height,
            layer_count_or_depth = (uint)data.GetLength(0),
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1
        };

        texture = SDL_CreateGPUTexture(device.Handle, &createInfo);
        if (texture == null)
        {
            SdlRuntime.Throw("Failed to create GPU texture");
        }   
    }
    
    private bool disposed;
    public void Dispose()
    {
        if (disposed)
            return;

        device.ReleaseTexture(texture);
        disposed = true;
    }
}
using SharpCraft.Platform;
using SDL;
using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class Texture : IDisposable
{
    public SDL_GPUTexture* Handle => texture;
    public byte[] Data { get; }

    public uint Width { get; }
    public uint Height { get; }

    private readonly GpuDevice device;
    private readonly SDL_GPUTexture* texture;

    public Texture(GpuDevice device, uint width, uint height, byte[] data)
    {
        uint expectedByteCount = checked(width * height * 4);

        if ((uint)data.Length != expectedByteCount)
        {
            throw new ArgumentException(
                $"Texture byte count mismatch. Expected {expectedByteCount}, got {data.Length}.",
                nameof(data)
            );
        }
        
        this.device = device;

        Width = width;
        Height = height;

        Data = data;

        SDL_GPUTextureCreateInfo createInfo = new()
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,

            // RGBA8 pixel data from StbImageSharp
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,

            // Required if the texture will be sampled in shaders
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,

            width = width,
            height = height,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1
        };

        texture = SDL_CreateGPUTexture(device.Handle, &createInfo);
        if (texture == null)
        {
            SdlRuntime.Throw("Failed to create GPU texture");
        }   
    }

    public Texture(GpuDevice device, uint width, uint height, Rgba color) :
        this(device, width, height, CreateColorData(width, height, color))
    {
    }
    
    private static byte[] CreateColorData(uint width, uint height, Rgba color)
    {
        int pixelCount = checked((int)(width * height));
        byte[] data = new byte[pixelCount * 4];

        for (int i = 0; i < data.Length; i += 4)
        {
            data[i + 0] = color.R;
            data[i + 1] = color.G;
            data[i + 2] = color.B;
            data[i + 3] = color.A;
        }

        return data;
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

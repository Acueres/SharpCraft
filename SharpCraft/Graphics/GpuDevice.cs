using SDL;

using SharpCraft.Platform;

using static SDL.SDL3;

namespace SharpCraft.Graphics;

internal unsafe class GpuDevice : IDisposable
{
    public SDL_GPUDevice* Handle => device;
    public SDL_GPUTextureFormat SwapchainFormat { get; private set; }

    public string DriverName { get; }
    public string DeviceName { get; }
    public GpuResourceUsage ResourceUsage => resources.Snapshot;

    private readonly SDL_GPUDevice* device;
    private readonly GpuResourceTracker resources = new();

    private readonly Window window;

    public GpuDevice(string driverName, Window window, bool debugInfo = false, bool debugMode = false)
    {
        this.window = window;

        device = SDL_CreateGPUDevice(
                SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                debugMode,
                driverName
            );

        if (device == null)
        {
            SdlRuntime.Throw("Failed to create GPU device");
        }

        if (!SDL_ClaimWindowForGPUDevice(device, window.Handle))
        {
            SdlRuntime.Throw("Failed to claim window for GPU device");
        }

        SwapchainFormat = SDL_GetGPUSwapchainTextureFormat(device, window.Handle);

        DriverName = SDL_GetGPUDeviceDriver(device)!;

        var gpuProperties = SDL_GetGPUDeviceProperties(device);
        DeviceName = SDL_GetStringProperty(gpuProperties, SDL_PROP_GPU_DEVICE_NAME_STRING, "unknown device")!;

        if (debugInfo)
        {
            Console.WriteLine($"Driver: {DriverName}");
            Console.WriteLine($"Device: {DeviceName}");
        }
    }

    public SDL_GPUBuffer* CreateBuffer(SDL_GPUBufferCreateInfo* info)
    {
        SDL_GPUBuffer* buffer = SDL_CreateGPUBuffer(device, info);
        if (buffer == null) return null;

        try
        {
            resources.AddBuffer((nint)buffer, info->size);
            return buffer;
        }
        catch
        {
            SDL_ReleaseGPUBuffer(device, buffer);
            throw;
        }
    }

    public void ReleaseBuffer(SDL_GPUBuffer* buffer)
    {
        resources.RemoveBuffer((nint)buffer);
        SDL_ReleaseGPUBuffer(device, buffer);
    }

    public SDL_GPUTexture* CreateTexture(SDL_GPUTextureCreateInfo* info)
    {
        long bytes = RequestedTextureBytes(info);
        SDL_GPUTexture* texture = SDL_CreateGPUTexture(device, info);
        if (texture == null) return null;

        try
        {
            resources.AddTexture((nint)texture, bytes);
            return texture;
        }
        catch
        {
            SDL_ReleaseGPUTexture(device, texture);
            throw;
        }
    }

    public void ReleaseTexture(SDL_GPUTexture* texture)
    {
        resources.RemoveTexture((nint)texture);
        SDL_ReleaseGPUTexture(device, texture);
    }

    private static long RequestedTextureBytes(SDL_GPUTextureCreateInfo* info)
    {
        int samples = info->sample_count switch
        {
            SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1 => 1,
            SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_2 => 2,
            SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_4 => 4,
            SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_8 => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(info), "Unknown GPU sample count")
        };

        long bytes = 0;
        uint width = info->width;
        uint height = info->height;
        uint layersOrDepth = info->layer_count_or_depth;
        for (uint level = 0; level < info->num_levels; level++)
        {
            bytes = checked(bytes + SDL_CalculateGPUTextureFormatSize(info->format, width, height, layersOrDepth) * samples);
            width = Math.Max(1u, width / 2);
            height = Math.Max(1u, height / 2);
            if (info->type == SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_3D)
            {
                layersOrDepth = Math.Max(1u, layersOrDepth / 2);
            }
        }

        return bytes;
    }

    public void WaitIdle()
    {
        SDL_WaitForGPUIdle(device);
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed || device == null) return;

        SDL_WaitForGPUIdle(device);

        SDL_ReleaseWindowFromGPUDevice(device, window.Handle);
        SDL_DestroyGPUDevice(device);
        disposed = true;
    }
}

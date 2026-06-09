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

    private readonly SDL_GPUDevice* device;

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

    public void ReleaseTexture(SDL_GPUTexture* texture)
    {
        SDL_ReleaseGPUTexture(device, texture);
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

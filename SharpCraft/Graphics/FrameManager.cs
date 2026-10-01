using SharpCraft.Platform;
using SharpCraft.Diagnostics;

using System.Diagnostics;
using SDL;
using static SDL.SDL3;

namespace SharpCraft.Graphics;

internal unsafe class FrameManager(GpuDevice device, Window window, FrameProfiler profiler)
{
    public bool TryBeginFrame(out FrameContext context)
    {
        context = default;

        SDL_GPUCommandBuffer* cmd = SDL_AcquireGPUCommandBuffer(device.Handle);
        if (cmd == null)
        {
            SdlRuntime.Throw("Failed to acquire command buffer");
        }

        SDL_GPUTexture* swapchainTexture = null;
        uint swapchainWidth = 0;
        uint swapchainHeight = 0;

        if (!SDL_AcquireGPUSwapchainTexture(
                cmd,
                window.Handle,
                &swapchainTexture,
                &swapchainWidth,
                &swapchainHeight))
        {
            SubmitEmpty(cmd);
            return false;
        }

        if (swapchainTexture == null)
        {
            SubmitEmpty(cmd);
            return false;
        }

        context = new FrameContext(cmd, swapchainTexture, swapchainWidth, swapchainHeight);

        return true;
    }

    public void SubmitFrame(FrameContext context)
    {
        if (!SDL_SubmitGPUCommandBuffer(context.CommandBuffer))
        {
            SdlRuntime.Throw("Failed to submit command buffer");
        }
    }

    private void SubmitEmpty(SDL_GPUCommandBuffer* commandBuffer)
    {
        long started = Stopwatch.GetTimestamp();
        bool submitted = SDL_SubmitGPUCommandBuffer(commandBuffer);
        profiler.Timings.SubmitMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (!submitted) SdlRuntime.Throw("Failed to submit empty command buffer");
    }
}

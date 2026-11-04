using SharpCraft.Platform;
using SharpCraft.Diagnostics;

using System.Diagnostics;
using SDL;
using static SDL.SDL3;

namespace SharpCraft.Graphics;

internal unsafe class FrameManager(GpuDevice device, Window window, SubmissionTracker submissionTracker, FrameProfiler profiler)
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

    public ulong SubmitFrame(FrameContext context)
    {
        return submissionTracker.Submit(context.CommandBuffer);
    }

    private void SubmitEmpty(SDL_GPUCommandBuffer* commandBuffer)
    {
        long started = Stopwatch.GetTimestamp();
        submissionTracker.Submit(commandBuffer);
        profiler.Timings.SubmitMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }
}

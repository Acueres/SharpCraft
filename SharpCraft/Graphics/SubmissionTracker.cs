using SharpCraft.Platform;

using SDL;
using static SDL.SDL3;

namespace SharpCraft.Graphics;

internal unsafe class SubmissionTracker(GpuDevice device)
{
    private readonly record struct Submission(ulong Id, nint Fence);

    public ulong LastSubmittedId { get; private set; }
    public ulong CompletedId { get; private set; }

    private readonly Queue<Submission> pending = [];

    public ulong Submit(SDL_GPUCommandBuffer* commandBuffer)
    {
        ulong id = checked(LastSubmittedId + 1);

        pending.EnsureCapacity(checked(pending.Count + 1));

        SDL_GPUFence* fence =
            SDL_SubmitGPUCommandBufferAndAcquireFence(commandBuffer);

        if (fence == null)
        {
            throw new InvalidOperationException(
                $"GPU submission failed: {SdlRuntime.GetError()}");
        }

        pending.Enqueue(new Submission(id, (nint)fence));
        LastSubmittedId = id;

        return id;
    }
    
    public void PollCompleted()
    {
        while (pending.TryPeek(out var submission))
        {
            SDL_GPUFence* fence = (SDL_GPUFence*)submission.Fence;

            if (!SDL_QueryGPUFence(device.Handle, fence))
            {
                break;
            }

            pending.Dequeue();
            SDL_ReleaseGPUFence(device.Handle, fence);

            CompletedId = submission.Id;
        }
    }

    public void WaitForCompletion(ulong submissionId)
    {
        var submission = pending.Single(s => s.Id == submissionId);
        
        SDL_GPUFence** fences = stackalloc SDL_GPUFence*[1];
        fences[0] = (SDL_GPUFence*)submission.Fence;

        if (!SDL_WaitForGPUFences(device.Handle, false, fences, 1))
        {
            SdlRuntime.Throw("Waiting for GPU fences failed");
        }
    }
}
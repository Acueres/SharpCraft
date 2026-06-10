using static SDL.SDL3;

namespace SharpCraft.Time;

internal sealed class FrameLimiter(uint targetFps)
{
    private readonly ulong targetFrameNs = 1_000_000_000UL / targetFps;
    private ulong nextFrameNs = SDL_GetTicksNS();

    public void Wait()
    {
        nextFrameNs += targetFrameNs;

        ulong nowNs = SDL_GetTicksNS();

        if (nowNs < nextFrameNs)
        {
            SDL_DelayPrecise(nextFrameNs - nowNs);
        }
        else
        {
            nextFrameNs = nowNs;
        }
    }
}
using SDL;
using static SDL.SDL3;

namespace SharpCraft.Platform;

internal class SdlRuntime : IDisposable
{
    public SdlRuntime()
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            Throw("SDL failed to initialize");
        }
    }

    public static void Throw(string message)
    {
        string error = SDL_GetError() ?? "unknown SDL error";
        throw new InvalidOperationException($"{message}: {error}");
    }
    
    public static string? GetError() => SDL_GetError();

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        SDL_Quit();

        disposed = true;
    }
}

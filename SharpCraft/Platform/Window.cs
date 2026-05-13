using SDL;
using static SDL.SDL3;

namespace SharpCraft.Platform;

internal unsafe class Window : IDisposable
{
    public SDL_Window* Handle => window;

    private readonly SDL_Window* window;

    public Window(string title, int width, int height)
    {
        window = SDL_CreateWindow(
        title,
        width,
        height,
        SDL_WindowFlags.SDL_WINDOW_RESIZABLE
    );

        if (window == null)
        {
            SdlRuntime.Throw("SDL failed to create window");
        }
    }

    public void SetRelativeMouseMode(bool enabled)
    {
        if (!SDL_SetWindowRelativeMouseMode(Handle, enabled))
        {
            SdlRuntime.Throw("Failed to set relative mouse mode");
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        SDL_DestroyWindow(window);

        disposed = true;
    }
}

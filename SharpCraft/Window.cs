using SDL;

using static SDL.SDL3;

namespace SharpCraft;

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

    public void Dispose()
    {
        SDL_DestroyWindow(window);
    }
}

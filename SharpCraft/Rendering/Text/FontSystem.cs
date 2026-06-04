using SharpCraft.Platform;

using static SDL.SDL3_ttf;

namespace SharpCraft.Rendering.Text;

internal sealed class FontSystem : IDisposable
{
    public FontSystem()
    {
        if (!TTF_Init())
        {
            SdlRuntime.Throw("Failed to initialize SDL_ttf");
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        TTF_Quit();
        disposed = true;
    }
}
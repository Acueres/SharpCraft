using SharpCraft.Platform;

using System.Text;
using SDL;

using static SDL.SDL3_ttf;

namespace SharpCraft.Rendering.Text;

internal unsafe class Font : IDisposable
{
    public TTF_Font* Handle => font;

    private readonly TTF_Font* font;
    
    public Font(string path, float size)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(path + '\0');

        fixed (byte* pPath = pathBytes)
        {
            font = TTF_OpenFont(pPath, size);
        }

        if (Handle == null)
        {
            SdlRuntime.Throw($"Failed to open font: {path}");
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        TTF_CloseFont(font);

        disposed = true;
    }
}
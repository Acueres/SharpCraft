using SharpCraft.Graphics;
using SharpCraft.Platform;

using SDL;
using static SDL.SDL3_ttf;

namespace SharpCraft.Rendering.Text;

internal sealed unsafe class GpuTextEngine : IDisposable
{
    private readonly TTF_TextEngine* handle;
    private readonly HashSet<TextLayout> texts = [];
    private bool disposed;

    public GpuTextEngine(GpuDevice device)
    {
        handle = TTF_CreateGPUTextEngine(device.Handle);
        if (handle == null)
        {
            SdlRuntime.Throw("Failed to create GPU text engine");
        }
    }

    public TextLayout CreateText(Font font, string content)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var text = new TextLayout(this, handle, font, content);
        texts.Add(text);
        return text;
    }

    internal void Release(TextLayout text) => texts.Remove(text);

    public void Dispose()
    {
        if (disposed) return;

        // Text objects must be destroyed before SDL_ttf releases their atlases
        while (texts.Count > 0)
        {
            texts.First().Dispose();
        }

        TTF_DestroyGPUTextEngine(handle);
        disposed = true;
    }
}

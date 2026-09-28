using SharpCraft.Platform;

using SDL;
using System.Text;
using static SDL.SDL3_ttf;

namespace SharpCraft.Rendering.Text;

internal sealed unsafe class TextLayout : IDisposable
{
    private readonly GpuTextEngine engine;
    private readonly TTF_Text* handle;
    private TTF_GPUAtlasDrawSequence* drawData;
    private bool dirty = true;
    private bool disposed;

    public Font Font { get; private set; }
    public string Content { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    internal TextLayout(GpuTextEngine engine, TTF_TextEngine* engineHandle, Font font, string content)
    {
        this.engine = engine;
        Font = font;
        Content = content;

        // Always terminate empty strings too: SDL_ttf treats length 0 as strlen
        byte[] bytes = Encoding.UTF8.GetBytes(content + '\0');
        fixed (byte* pText = bytes)
        {
            handle = TTF_CreateText(engineHandle, font.Handle, pText, (nuint)(bytes.Length - 1));
        }

        if (handle == null)
        {
            SdlRuntime.Throw("Failed to create text layout");
        }

        // Preserve explicit newlines without adding wrapping at a fixed width
        if (!TTF_SetTextWrapWidth(handle, 0))
        {
            TTF_DestroyText(handle);
            SdlRuntime.Throw("Failed to enable multiline text layout");
        }
    }

    public void Set(Font font, string content)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (Font != font)
        {
            Invalidate();
            if (!TTF_SetTextFont(handle, font.Handle))
            {
                SdlRuntime.Throw("Failed to change text font");
            }
            Font = font;
        }

        if (Content != content)
        {
            Invalidate();
            byte[] bytes = Encoding.UTF8.GetBytes(content + '\0');
            fixed (byte* pText = bytes)
            {
                if (!TTF_SetTextString(handle, pText, (nuint)(bytes.Length - 1)))
                {
                    SdlRuntime.Throw("Failed to change text content");
                }
            }
            Content = content;
        }
    }

    private void Invalidate()
    {
        dirty = true;
        drawData = null;
    }

    public void Prepare()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!dirty) return;

        // May submit glyph uploads; call this before recording the render pass
        if (!TTF_UpdateText(handle))
        {
            SdlRuntime.Throw("Failed to update text layout");
        }

        int width, height;
        if (!TTF_GetTextSize(handle, &width, &height))
        {
            SdlRuntime.Throw("Failed to measure text layout");
        }

        drawData = TTF_GetGPUTextDrawData(handle);
        Width = width;
        Height = height;
        dirty = false;
    }

    internal TTF_GPUAtlasDrawSequence* DrawData
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (dirty)
            {
                throw new InvalidOperationException("Prepare text before drawing it.");
            }
            // SDL_ttf owns this geometry until the text is updated or destroyed
            return drawData;
        }
    }

    public void Dispose()
    {
        if (disposed) return;

        TTF_DestroyText(handle);
        drawData = null;
        engine.Release(this);
        disposed = true;
    }
}

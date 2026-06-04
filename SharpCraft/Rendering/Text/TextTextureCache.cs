using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;

namespace SharpCraft.Rendering.Text;

internal sealed class TextTextureCache : IDisposable
{
    private readonly GpuDevice device;
    private readonly GpuUploader uploader;

    private readonly Dictionary<TextTextureKey, Texture> textures = [];

    public TextTextureCache(GpuDevice device, GpuUploader uploader)
    {
        this.device = device;
        this.uploader = uploader;
    }

    public Texture GetOrCreate(Font font, string text)
    {
        TextTextureKey key = new(font, text);

        if (textures.TryGetValue(key, out Texture? texture))
        {
            return texture;
        }

        TextBitmap bitmap = TextRasterizer.Render(font, text);

        texture = new Texture(
            device,
            bitmap.Width,
            bitmap.Height,
            bitmap.Pixels
        );

        uploader.Upload(texture);

        textures.Add(key, texture);

        return texture;
    }

    public void Clear()
    {
        foreach (Texture texture in textures.Values)
        {
            texture.Dispose();
        }

        textures.Clear();
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        Clear();

        disposed = true;
    }

    private readonly record struct TextTextureKey(
        Font Font,
        string Text
    );
}
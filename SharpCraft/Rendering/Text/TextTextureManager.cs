using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;

namespace SharpCraft.Rendering.Text;

internal class TextTextureManager(GpuDevice device, GpuUploader uploader)
{
    private readonly Dictionary<TextTextureKey, Texture> staticTextures = [];
    private readonly Queue<TextureDisposal> textureDisposalQueue = [];

    public DynamicTextSlot CreateDynamic(Font font, string text)
    {
        var texture = CreateTexture(font, text);
        return new DynamicTextSlot(font, text, texture);
    }

    public void UpdateDynamic(Font font, string text, DynamicTextSlot dynamicText)
    {
        if (dynamicText.Font != font || dynamicText.Text != text)
        {
            textureDisposalQueue.Enqueue(new TextureDisposal(dynamicText.Texture, 0));

            var texture = CreateTexture(font, text);
            dynamicText.Set(font, text, texture);
        }
    }

    public Texture GetOrCreateStatic(Font font, string text)
    {
        TextTextureKey key = new(font, text);

        if (staticTextures.TryGetValue(key, out Texture? texture))
        {
            return texture;
        }

        texture = CreateTexture(font, text);

        staticTextures.Add(key, texture);

        return texture;
    }

    private Texture CreateTexture(Font font, string text)
    {
        TextBitmap bitmap = TextRasterizer.Render(font, text);

        var texture = new Texture(
            device,
            bitmap.Width,
            bitmap.Height,
            bitmap.Pixels
        );

        uploader.Upload(texture);

        return texture;
    }

    public void FlushDynamic()
    {
        int count = textureDisposalQueue.Count;

        for (int i = 0; i < count; i++)
        {
            var textureDisposal = textureDisposalQueue.Dequeue();
            if (textureDisposal.FramesAge >= 3)
            {
                textureDisposal.Texture.Dispose();
            }
            else
            {
                byte framesAge = (byte)(textureDisposal.FramesAge + 1);
                textureDisposalQueue.Enqueue(new TextureDisposal(textureDisposal.Texture, framesAge));
            }
        }
    }

    public void ClearStatic()
    {
        foreach (Texture texture in staticTextures.Values)
        {
            texture.Dispose();
        }

        staticTextures.Clear();
    }

    private void ClearDynamic()
    {
        while (textureDisposalQueue.TryDequeue(out var textureDisposal))
        {
            textureDisposal.Texture.Dispose();
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        ClearStatic();
        ClearDynamic();

        disposed = true;
    }

    private readonly record struct TextTextureKey(
        Font Font,
        string Text
    );

    private readonly record struct TextureDisposal(
        Texture Texture,
        byte FramesAge
    );
}

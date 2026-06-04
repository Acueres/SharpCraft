namespace SharpCraft.Rendering.Text;

internal readonly record struct FontKey(
    string Path,
    float Size
);

internal sealed class FontLibrary : IDisposable
{
    private readonly Dictionary<FontKey, Font> fonts = [];

    public Font Get(string path, float size)
    {
        FontKey key = new(path, size);

        if (fonts.TryGetValue(key, out Font? font))
        {
            return font;
        }

        font = new Font(path, size);
        fonts.Add(key, font);

        return font;
    }
    
    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        foreach (Font font in fonts.Values)
        {
            font.Dispose();
        }

        fonts.Clear();

        disposed = true;
    }
}
using SharpCraft.Graphics.Resources;

namespace SharpCraft.Rendering.Text;

internal class DynamicTextSlot(Font font, string text, Texture texture)
{
    public Font Font { get; private set; } = font;
    public string Text { get; private set; } = text;
    public Texture Texture { get; private set; } = texture;

    internal void Set(Font font, string text, Texture texture)
    {
        Font = font;
        Text = text;
        Texture = texture;
    }
}

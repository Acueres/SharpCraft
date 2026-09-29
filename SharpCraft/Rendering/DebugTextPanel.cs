using SharpCraft.Graphics;
using SharpCraft.Rendering.Text;
using System.Numerics;

namespace SharpCraft.Rendering;

internal sealed class DebugTextPanel : IDisposable
{
    private static readonly Vector4 HeaderColor = Colors.LightBlue.ToVector4();
    private static readonly Vector4 LabelColor = Colors.LightGray.ToVector4();
    private static readonly Vector4 ValueColor = Colors.WhiteSmoke.ToVector4();
    private static readonly Vector4 ShadowColor = new(0, 0, 0, 0.9f);
    private readonly TextLayout title;
    private readonly TextLayout measure;
    private readonly TextLayout[] labels;
    private readonly TextLayout[] values;
    private readonly int labelCharacters;
    private readonly int widthCharacters;
    private Font font;
    private float valueOffset;
    private float rowHeight;

    public Vector2 Size { get; private set; }

    public DebugTextPanel(GpuTextEngine engine, Font font, string heading, string[] rowLabels,
        int labelCharacters, int widthCharacters)
    {
        this.font = font;
        this.labelCharacters = labelCharacters;
        this.widthCharacters = widthCharacters;
        title = engine.CreateText(font, heading);
        // Reserve columns using the current monospace font, independently of changing values.
        measure = engine.CreateText(font, new string('0', widthCharacters));
        labels = new TextLayout[rowLabels.Length];
        values = new TextLayout[rowLabels.Length];
        for (int i = 0; i < rowLabels.Length; i++)
        {
            labels[i] = engine.CreateText(font, rowLabels[i]);
            values[i] = engine.CreateText(font, "—");
        }
    }

    public void SetValue(int row, string value) => values[row].Set(font, value);

    public void Rescale(Font newFont)
    {
        font = newFont;
        title.Set(font, title.Content);
        measure.Set(font, measure.Content);
        foreach (var label in labels) label.Set(font, label.Content);
        foreach (var value in values) value.Set(font, value.Content);
        Size = default;
    }

    public void Prepare()
    {
        title.Prepare();
        measure.Prepare();
        float characterWidth = (float)measure.Width / widthCharacters;
        valueOffset = characterWidth * labelCharacters;
        rowHeight = Math.Max(title.Height, measure.Height) + 2;
        float width = Math.Max(measure.Width, title.Width);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].Prepare();
            values[i].Prepare();
            rowHeight = Math.Max(rowHeight, Math.Max(labels[i].Height, values[i].Height) + 2);
            width = Math.Max(width, Offset(i) + values[i].Width);
        }
        // Width grows only if an exceptional value exceeds its reserved column.
        Size = new(Math.Max(Size.X, width), rowHeight * (labels.Length + 1) + 4);
    }

    private float Offset(int row) => labels[row].Content.Length == 0 ? 0 : valueOffset;

    public void Draw(SpriteRenderer renderer, Vector2 position, float scale)
    {
        DrawPass(renderer, position + Vector2.One, scale, shadow: true);
        DrawPass(renderer, position, scale, shadow: false);
    }

    private void DrawPass(SpriteRenderer renderer, Vector2 position, float scale, bool shadow)
    {
        renderer.DrawText(title, position, scale, shadow ? ShadowColor : HeaderColor);
        for (int i = 0; i < labels.Length; i++)
        {
            Vector2 rowPosition = position + new Vector2(0, rowHeight * (i + 1) + 4) * scale;
            renderer.DrawText(labels[i], rowPosition, scale, shadow ? ShadowColor : LabelColor);
            renderer.DrawText(values[i], rowPosition + new Vector2(Offset(i) * scale, 0), scale,
                shadow ? ShadowColor : ValueColor);
        }
    }

    public void Dispose()
    {
        title.Dispose();
        measure.Dispose();
        foreach (var label in labels) label.Dispose();
        foreach (var value in values) value.Dispose();
    }
}

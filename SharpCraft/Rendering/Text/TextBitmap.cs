namespace SharpCraft.Rendering.Text;

internal readonly record struct TextBitmap(
    uint Width,
    uint Height,
    byte[] Pixels
);
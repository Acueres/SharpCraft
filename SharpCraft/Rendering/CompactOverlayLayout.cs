using System.Numerics;

namespace SharpCraft.Rendering;

internal readonly record struct CompactOverlayLayout(Vector2 View, Vector2 Systems, Vector2 Gpu, float Scale)
{
    public static CompactOverlayLayout Calculate(Vector2 viewport, float padding,
        Vector2 view, Vector2 systems, Vector2 gpu)
    {
        float margin = Math.Min(padding, Math.Min(viewport.X, viewport.Y) / 8);
        float gap = margin * 2;
        float width = Math.Max(1, viewport.X - margin * 2);
        float height = Math.Max(1, viewport.Y - margin * 2);

        if (view.X + systems.X + gpu.X + gap * 2 <= width)
        {
            float scale = Math.Min(1, height / Math.Max(view.Y, Math.Max(systems.Y, gpu.Y)));
            float leftEnd = margin + view.X * scale;
            float right = viewport.X - margin - gpu.X * scale;
            float center = Math.Clamp((viewport.X - systems.X * scale) / 2,
                leftEnd + gap, right - gap - systems.X * scale);
            return new(new(margin, margin), new(center, margin), new(right, margin), scale);
        }

        // Reflow before shrinking the font: view and systems above, GPU below systems.
        if (view.X + Math.Max(systems.X, gpu.X) + gap <= width)
        {
            float contentHeight = Math.Max(view.Y, systems.Y + gpu.Y + gap);
            float scale = Math.Min(1, height / contentHeight);
            float right = viewport.X - margin - Math.Max(systems.X, gpu.X) * scale;
            return new(new(margin, margin), new(right, margin),
                new(right, margin + (systems.Y + gap) * scale), scale);
        }

        float maxWidth = Math.Max(view.X, Math.Max(systems.X, gpu.X));
        float stackedHeight = view.Y + systems.Y + gpu.Y + gap * 2;
        float stackedScale = Math.Min(1, Math.Min(width / maxWidth, height / stackedHeight));
        return new(new(margin, margin), new(margin, margin + (view.Y + gap) * stackedScale),
            new(margin, margin + (view.Y + systems.Y + gap * 2) * stackedScale), stackedScale);
    }
}

using SharpCraft.Diagnostics;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering.Text;
using SharpCraft.SharpMath;

using System.Diagnostics;
using System.Numerics;

namespace SharpCraft.Rendering;

internal sealed class DebugOverlay : IDisposable
{
    private readonly TextLayout text;
    private readonly Texture background;
    private readonly string deviceInfo;
    
    private Font font;
    private bool visible = true;
    private long displayedRevision;
    private long memoryMeasuredAt;
    private double managedMiB;
    private double processMiB;
    private int gen0;
    private int gen1;
    private int gen2;
    private bool disposed;

    public DebugOverlay(GpuTextEngine textEngine, Font font, GpuDevice device, GpuUploader uploader)
    {
        this.font = font;
        deviceInfo = $"{device.DriverName} | {device.DeviceName}";
        text = textEngine.CreateText(font, $"SharpCraft profiler | F3 hide\nCollecting samples...\n{deviceInfo}");
        background = new Texture(device, 1, 1, Colors.White);
        uploader.Upload(background);
    }

    public void Toggle() => visible = !visible;

    public void Update(in FrameProfile profile)
    {
        if (!visible || profile.Revision == 0 || profile.Revision == displayedRevision) return;

        long now = Stopwatch.GetTimestamp();
        
        if (memoryMeasuredAt == 0 || Stopwatch.GetElapsedTime(memoryMeasuredAt, now).TotalSeconds >= 1)
        {
            managedMiB = GC.GetTotalMemory(forceFullCollection: false) / 1048576.0;
            using var process = Process.GetCurrentProcess();
            processMiB = process.PrivateMemorySize64 / 1048576.0;
            gen0 = GC.CollectionCount(0);
            gen1 = GC.CollectionCount(1);
            gen2 = GC.CollectionCount(2);
            memoryMeasuredAt = now;
        }

        string content = CompactProfileText.Format(profile, managedMiB, processMiB, gen0, gen1, gen2);
        text.Set(font, $"{content}\n{deviceInfo}");
        displayedRevision = profile.Revision;
    }

    public void Rescale(Font newFont)
    {
        font = newFont;
        text.Set(newFont, text.Content);
    }

    public void Prepare()
    {
        if (visible) text.Prepare();
    }

    public void Draw(SpriteRenderer spriteRenderer, uint screenWidth, uint screenHeight, float padding)
    {
        if (!visible) return;

        // Preserve aspect ratio when the text exceeds a small viewport.
        float availableWidth = Math.Max(1, screenWidth - padding * 4);
        float availableHeight = Math.Max(1, screenHeight - padding * 4);
        float scale = Math.Min(1, Math.Min(availableWidth / Math.Max(1, text.Width), availableHeight / Math.Max(1, text.Height)));
        float width = text.Width * scale;
        float height = text.Height * scale;

        spriteRenderer.Draw(background,
            new Rect(padding, padding, width + padding * 2, height + padding * 2),
            new Vector4(0.025f, 0.035f, 0.05f, 0.82f));
        spriteRenderer.DrawText(text,
            new Vector2(padding * 2, padding * 2), scale,
            Colors.WhiteSmoke.ToVector4());
    }

    public void Dispose()
    {
        if (disposed) return;
        text.Dispose();
        background.Dispose();
        disposed = true;
    }
}

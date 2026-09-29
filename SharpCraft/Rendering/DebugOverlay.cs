using SharpCraft.Diagnostics;
using SharpCraft.Graphics;
using SharpCraft.Rendering.Text;
using SharpCraft.World.Chunks;

using System.Diagnostics;
using System.Numerics;

namespace SharpCraft.Rendering;

internal sealed class DebugOverlay : IDisposable
{
    private readonly DebugTextPanel view;
    private readonly DebugTextPanel systems;
    private readonly DebugTextPanel gpu;
    private bool visible = true;
    private long displayedRevision;
    private long coordinatesMeasuredAt;
    private long memoryMeasuredAt;
    private bool disposed;

    public DebugOverlay(GpuTextEngine engine, Font font, GpuDevice device)
    {
        view = new(engine, font, "VIEW", ["", "XYZ", "Chunk"], 6, 37);
        systems = new(engine, font, "WORLD / CPU", ["Chunks", "Queue", "Work", "Heap", "Process"], 8, 33);
        gpu = new(engine, font, $"GPU · {device.DriverName}", ["", "Tracked", "Faces", "Terrain", "Upload"], 8, 33);
        gpu.SetValue(0, CompactProfileText.Truncate(device.DeviceName, 33));
    }

    public void Toggle()
    {
        visible = !visible;
        if (!visible) return;
        displayedRevision = 0;
        coordinatesMeasuredAt = 0;
        memoryMeasuredAt = 0;
    }

    public void Update(in FrameProfile profile, Vector3 cameraPosition)
    {
        if (!visible) return;
        long now = Stopwatch.GetTimestamp();

        if (coordinatesMeasuredAt == 0 || Stopwatch.GetElapsedTime(coordinatesMeasuredAt, now).TotalMilliseconds >= 100)
        {
            view.SetValue(1, CompactProfileText.Position(cameraPosition));
            view.SetValue(2, CompactProfileText.ChunkIndex(Chunk.WorldToChunkCoords(cameraPosition)));
            coordinatesMeasuredAt = now;
        }

        if (memoryMeasuredAt == 0 || Stopwatch.GetElapsedTime(memoryMeasuredAt, now).TotalSeconds >= 1)
        {
            double managedMiB = GC.GetTotalMemory(forceFullCollection: false) / 1048576.0;
            using var process = Process.GetCurrentProcess();
            double processMiB = process.PrivateMemorySize64 / 1048576.0;
            systems.SetValue(3, FormattableString.Invariant($"{managedMiB:0.0} MiB"));
            systems.SetValue(4, FormattableString.Invariant($"{processMiB:0.0} MiB"));
            memoryMeasuredAt = now;
        }

        if (profile.Revision == 0 || profile.Revision == displayedRevision) return;
        var rendering = profile.Rendering;
        var streaming = profile.Streaming;
        view.SetValue(0, CompactProfileText.Frame(profile));
        systems.SetValue(0, FormattableString.Invariant($"{rendering.ResidentChunks:N0} loaded · {rendering.VisibleChunks:N0} visible"));
        systems.SetValue(1, FormattableString.Invariant($"G {streaming.GenerationQueued} / L {streaming.LightingQueued} / M {streaming.MeshingQueued}"));
        systems.SetValue(2, FormattableString.Invariant($"{profile.Timings.WorkMilliseconds:0.0} ms"));
        gpu.SetValue(1, FormattableString.Invariant($"{profile.GpuResources.TotalBytes / 1048576.0:0.0} MiB"));
        gpu.SetValue(2, CompactProfileText.Count((ulong)rendering.OpaqueFaces + rendering.TransparentFaces));
        gpu.SetValue(3, FormattableString.Invariant($"{rendering.TerrainUsedBytes / 1048576.0:0.0} / {rendering.TerrainBufferBytes / 1048576.0:0.0} MiB"));
        gpu.SetValue(4, FormattableString.Invariant($"{profile.TerrainUploadBytesPerFrame / 1024:0.0} KiB/frame"));
        displayedRevision = profile.Revision;
    }

    public void Rescale(Font font)
    {
        view.Rescale(font);
        systems.Rescale(font);
        gpu.Rescale(font);
    }

    public void Prepare()
    {
        if (!visible) return;
        view.Prepare();
        systems.Prepare();
        gpu.Prepare();
    }

    public void Draw(SpriteRenderer renderer, uint width, uint height, float padding)
    {
        if (!visible) return;
        var layout = CompactOverlayLayout.Calculate(new(width, height), padding, view.Size, systems.Size, gpu.Size);
        view.Draw(renderer, layout.View, layout.Scale);
        systems.Draw(renderer, layout.Systems, layout.Scale);
        gpu.Draw(renderer, layout.Gpu, layout.Scale);
    }

    public void Dispose()
    {
        if (disposed) return;
        view.Dispose();
        systems.Dispose();
        gpu.Dispose();
        disposed = true;
    }
}

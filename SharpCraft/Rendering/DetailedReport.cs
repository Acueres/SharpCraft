using SharpCraft.Diagnostics;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering.Text;
using SharpCraft.SharpMath;
using SharpCraft.World.Chunks;

using System.Globalization;
using System.Numerics;

namespace SharpCraft.Rendering;

internal sealed class DetailedReport : IDisposable
{
    private static readonly string[] PageNames = ["FRAME / CPU", "STREAMING", "RENDERER / RESOURCES", "BENCHMARKS"];

    private static readonly string[] CpuNames =
    [
        "Main work (before limiter)", "Frame limiter", "World / scheduler", "  Completion handling",
        "Renderer incl. UI", "  Culling", "  Face-list assembly", "  Terrain upload total", "    Upload staging (all)",
        "    Upload commands (all)", "  Scene commands", "Submit (all commands)", "Swapchain acquire",
        "UI prepare / upload"
    ];

    private readonly FrameProfiler profiler;
    private readonly GpuDevice device;
    private readonly BenchmarkOptions options;
    private readonly BenchmarkRun benchmark;
    private readonly DebugPanelState state;
    private readonly TextLayout[] headers;
    private readonly TextLayout[] lines;
    private readonly TextLayout footer;
    private readonly TextLayout measure;
    private readonly Texture pixel;
    private readonly List<string> rows = [];
    private readonly List<string> wrapped = [];
    private readonly FrameSample[] history = new FrameSample[256];
    private Font font;
    private long displayedRevision = -1;
    private int stateRevision = -1;
    private int columns;
    private int visibleRows;
    private int historyCount;
    private float lineHeight;
    private float chartMaximum;
    private Vec3<double> position;
    private uint width, height;
    private float padding;
    private DetailedReportLayout layout;

    public DetailedReport(GpuTextEngine engine, Font font, GpuDevice device, GpuUploader uploader,
        FrameProfiler profiler, DebugPanelState state, BenchmarkOptions options, BenchmarkRun benchmark)
    {
        this.font = font;
        this.device = device;
        this.profiler = profiler;
        this.state = state;
        this.options = options;
        this.benchmark = benchmark;
        headers = Enumerable.Range(0, 4).Select(_ => engine.CreateText(font, "")).ToArray();
        lines = Enumerable.Range(0, 48).Select(_ => engine.CreateText(font, "")).ToArray();
        footer = engine.CreateText(font, "");
        measure = engine.CreateText(font, new string('0', 100));
        pixel = new Texture(device, 1, 1, new byte[] { 255, 255, 255, 255 });
        uploader.Upload(pixel);
    }

    public void Update(Vec3<int> cameraPositionIndex, Vector3 cameraLocalPosition) =>
        position = new Vec3<double>(cameraPositionIndex.X, cameraPositionIndex.Y, cameraPositionIndex.Z) * Chunk.Size
                   + new Vec3<double>(cameraLocalPosition.X, cameraLocalPosition.Y, cameraLocalPosition.Z);

    public void Rescale(Font newFont)
    {
        font = newFont;
        foreach (var text in headers.Concat(lines).Append(footer).Append(measure)) text.Set(font, text.Content);
        displayedRevision = -1;
    }

    public void Prepare(uint viewportWidth, uint viewportHeight, float inset)
    {
        if (!state.Detailed) return;
        measure.Prepare();
        float charWidth = measure.Width / 100f;
        lineHeight = measure.Height + 3;
        layout = DetailedReportLayout.Calculate(viewportWidth, viewportHeight, inset, charWidth, lineHeight,
            lines.Length);
        bool resized = width != viewportWidth || height != viewportHeight || padding != inset;
        width = viewportWidth;
        height = viewportHeight;
        padding = inset;
        if (profiler.Snapshot.Revision != displayedRevision || state.Revision != stateRevision || resized)
        {
            columns = layout.Columns;
            visibleRows = layout.Rows;
            Rebuild();
            displayedRevision = profiler.Snapshot.Revision;
            stateRevision = state.Revision;
        }

        foreach (var header in headers) header.Prepare();
        for (int i = 0; i < visibleRows; i++) lines[i].Prepare();
        footer.Prepare();
    }

    private static string F(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
    private static string MiB(double bytes) => F($"{bytes / 1048576:0.00} MiB");

    private void Rebuild()
    {
        var profile = profiler.Snapshot;
        var frame = profiler.FrameStatistics;
        headers[0].Set(font, Fit($"SharpCraft detailed report | {PageNames[state.Page]} | Shift+F3 close"));
        headers[1].Set(font,
            Fit(F(
                $"{(frame.Mean > 0 ? 1000 / frame.Mean : 0):0.0} FPS | Frame {frame.Mean:0.00} ms | P95 {frame.P95:0.00} | Max {frame.Maximum:0.00} | {frame.Count} samples")));
        headers[2].Set(font,
            Fit(F(
                $"{options.Scene?.ToString() ?? "Interactive"} | Radius {options.Radius} | CPU cap {(options.FpsLimit == 0 ? "off" : $"{options.FpsLimit} FPS")} / {device.PresentationMode} | XYZ {position.X:0.0}, {position.Y:0.0}, {position.Z:0.0}")));
        historyCount = profiler.CopyHistory(history);
        chartMaximum = (float)Math.Max(33.34, frame.P95 * 1.5);
        headers[3].Set(font,
            Fit(historyCount == 0
                ? "Waiting for frame samples..."
                : F(
                    $"Frame history #{history[0].Id}..#{history[historyCount - 1].Id} | scale 0..{chartMaximum:0.0} ms | line 16.67 ms")));

        rows.Clear();
        switch (state.Page)
        {
            case 0: CpuRows(); break;
            case 1: StreamingRows(profile.Streaming); break;
            case 2: RendererRows(profile); break;
            case 3: BenchmarkRows(); break;
        }

        wrapped.Clear();
        foreach (string row in rows)
        {
            if (row.Length == 0)
            {
                wrapped.Add("");
                continue;
            }

            for (int start = 0; start < row.Length; start += columns)
                wrapped.Add(row.Substring(start, Math.Min(columns, row.Length - start)));
        }

        state.ClampScroll(wrapped.Count - visibleRows);
        for (int i = 0; i < visibleRows; i++)
            lines[i].Set(font, i + state.Scroll < wrapped.Count ? wrapped[i + state.Scroll] : "");
        footer.Set(font,
            Fit(F(
                $"1 CPU | 2 Streaming | 3 Renderer | 4 Benchmarks | Up/Down scroll {state.Scroll + 1}..{Math.Min(wrapped.Count, state.Scroll + visibleRows)}/{wrapped.Count}")));
    }

    private string Fit(string value) => value.Length <= columns ? value : value[..Math.Max(0, columns - 1)] + "…";

    private void CpuRows()
    {
        rows.Add("CPU wall time, ms / frame             Mean      Min      P95      Max");
        for (int i = 0; i < (int)CpuMetric.Count; i++)
        {
            var s = profiler.Statistics((CpuMetric)i);
            rows.Add(F($"{CpuNames[i],-35} {s.Mean,8:0.000} {s.Minimum,8:0.000} {s.P95,8:0.000} {s.Maximum,8:0.000}"));
        }

        var memory = profiler.Memory;
        rows.Add("");
        rows.Add(F($"Managed heap {MiB(memory.ManagedBytes)} | Process private {MiB(memory.ProcessBytes)}"));
        rows.Add(F(
            $"Main-thread allocations {profiler.Snapshot.MainThreadAllocatedBytesPerFrame / 1024:0.00} KiB/frame (250ms average)"));
        rows.Add(F(
            $"GC collections since launch: G0 {memory.Gen0Collections} / G1 {memory.Gen1Collections} / G2 {memory.Gen2Collections}"));
        rows.Add($"All-thread allocations since launch: {MiB(memory.TotalAllocatedBytes)} (approximate)");
        rows.Add("Scopes overlap: world includes completions; renderer includes its subscopes.");
        rows.Add("Upload scopes include terrain and UI; worker and GPU time are separate.");
        rows.Add("CPU wall time can include driver waits. These numbers are not GPU durations.");
    }

    private void StreamingRows(StreamingStatistics streaming)
    {
        rows.Add("Stage        Queue  Running/Workers    Done/s     Completed  Failed  Skipped");
        JobRow("Generation", streaming.GenerationQueued, streaming.Generation);
        JobRow("Lighting", streaming.LightingQueued, streaming.Lighting);
        JobRow("Meshing", streaming.MeshingQueued, streaming.Meshing);
        rows.Add(F(
            $"Results awaiting main thread: {streaming.ResultsQueued} | Stale results discarded: {streaming.StaleResults}"));
        rows.Add("");
        rows.Add("Job duration / queue wait, ms          Mean      P95      Max      Oldest queued");
        JobDuration("Generation", streaming.Generation);
        JobDuration("Lighting", streaming.Lighting);
        JobDuration("Meshing", streaming.Meshing);
        rows.Add("");
        rows.Add("Durations: last 128 finished jobs. Queue wait: last 128 started jobs.");
        rows.Add("Duration includes result/relight publishing and any channel backpressure.");
        rows.Add("Queues exclude running jobs; concurrent readings are approximate.");
        rows.Add("Completed counts successful execution, even if later rejected as stale.");
        rows.Add("Startup bulk generation is excluded from worker statistics.");
    }

    private void JobRow(string name, int queued, JobStatistics s) => rows.Add(
        F(
            $"{name,-11} {queued,6} {s.Running,5}/{s.Workers,-7} {s.PerSecond,9:0.0} {s.Completed,13} {s.Failed,7} {s.Skipped,8}"));

    private void JobDuration(string name, JobStatistics s)
    {
        rows.Add(F(
            $"{name + " job",-35} {s.Duration.Mean,8:0.000} {s.Duration.P95,8:0.000} {s.Duration.Maximum,8:0.000} {s.OldestQueuedMilliseconds,12:0.0}"));
        rows.Add(F(
            $"{name + " queue wait",-35} {s.QueueWait.Mean,8:0.000} {s.QueueWait.P95,8:0.000} {s.QueueWait.Maximum,8:0.000}"));
    }

    private void RendererRows(FrameProfile profile)
    {
        var r = profile.Rendering;
        var g = profile.GpuResources;
        rows.Add($"Device: {device.DeviceName} ({device.DriverName})");
        rows.Add($"GPU opaque / transparent timings: {device.TimingStatus}");
        rows.Add("");
        rows.Add(F($"Chunks resident / visible: {r.ResidentChunks:N0} / {r.VisibleChunks:N0} (current terrain path)"));
        rows.Add(F($"Face list opaque / transparent: {r.OpaqueFaces:N0} / {r.TransparentFaces:N0}"));
        rows.Add(F(
            $"Faces submitted, latest sampled frame: {r.SubmittedOpaqueFaces:N0} / {r.SubmittedTransparentFaces:N0}"));
        rows.Add(F(
            $"Draws terrain / UI: {r.TerrainDrawCalls} / {r.UiDrawCalls} | Skipped swapchain frame: {r.SkippedFrames}"));
        rows.Add(F(
            $"Uploads, latest sampled frame: {r.UploadJobs} jobs / {(r.TerrainUploadBytes + r.UiUploadBytes) / 1024.0:0.00} KiB"));
        rows.Add(F(
            $"Upload average: terrain {profile.TerrainUploadBytesPerFrame / 1024:0.00} / UI {profile.UiUploadBytesPerFrame / 1024:0.00} KiB/frame"));
        rows.Add("Upload queue: N/A (immediate submission; no upload scheduler)");
        rows.Add("");
        rows.Add($"Tracked persistent resources: {MiB(g.TotalBytes)}");
        rows.Add(F(
            $"Buffers: {g.BufferCount} / {MiB(g.BufferBytes)} | Textures: {g.TextureCount} / {MiB(g.TextureBytes)}"));
        rows.Add($"Terrain requested / reserved: {MiB(r.TerrainUsedBytes)} / {MiB(r.TerrainBufferBytes)}");
        rows.Add(F(
            $"Terrain capacity utilization: {(r.TerrainBufferBytes == 0 ? 0 : r.TerrainUsedBytes * 100.0 / r.TerrainBufferBytes):0.0}% | Unused {MiB(r.TerrainBufferBytes - r.TerrainUsedBytes)}"));
        rows.Add("Tracked bytes exclude driver overhead, staging, cycling, swapchain and SDL_ttf atlases.");
        rows.Add("");
        rows.Add("Indirect commands / list construction: N/A (Phase 3)");
        rows.Add("Slots / pages / fragmentation / deferred frees: N/A (Phase 2)");
        rows.Add("Capacity slack above describes whole face buffers, not an arena allocator.");
    }

    private void BenchmarkRows()
    {
        rows.Add("Phase 0 presets: A Exposure | B Merge-friendly | C Seeded terrain");
        rows.Add(F(
            $"Scene: {options.Scene?.ToString() ?? "none (interactive)"} | Seed: {options.Seed} | Chunk radius: {options.Radius}"));
        rows.Add(F(
            $"Camera: {options.Path} | Warmup: {options.WarmupSeconds:0.0}s | Measurement: {options.MeasureSeconds:0.0}s"));
        rows.Add(F(
            $"Status: {benchmark.Phase} | Remaining {benchmark.RemainingSeconds:0.0}s | Measured frames {benchmark.Frames:N0}"));
        rows.Add("");
        rows.Add(F(
            $"Frame mean / P95 (~0.1ms bins) / max: {benchmark.MeanMilliseconds:0.000} / {benchmark.P95Milliseconds:0.0} / {benchmark.MaximumMilliseconds:0.000} ms"));
        rows.Add(F(
            $"Main work: {benchmark.MeanWorkMilliseconds:0.000} ms/frame | Skipped swapchain frames: {benchmark.SkippedFrames}"));
        rows.Add(F(
            $"Uploads: {benchmark.UploadBytesPerFrame / 1024:0.00} KiB/frame | Main-thread allocation: {benchmark.AllocatedBytesPerFrame / 1024:0.00} KiB/frame"));
        rows.Add("P95 at/above 1000ms is shown as Infinity (histogram overflow).");
        rows.Add("");
        rows.Add("Run from the command line; restart with the same options to compare commits:");
        rows.Add("--benchmark exposure --seed 1337 --radius 4 --path rotate --fps 0");
        rows.Add("--benchmark merge --seed 1337 --radius 4 --path rotate --fps 0");
        rows.Add("--benchmark terrain --seed 1337 --radius 4 --path travel --fps 0");
        rows.Add("Repeat terrain at radii 2, 4, 8. Travel moves +X at 8 blocks/sec.");
        rows.Add("Loading is excluded. Sampling starts after warmup; whole boundary frames are kept.");
        rows.Add("Report overhead is part of the measurement. Keep UI, resolution and cap identical.");
        rows.Add("Terrain uses the current sandstone/lighting path; content grows with worldgen.");
        rows.Add("Fine detail, vegetation and dynamic stress presets belong to later phases.");
    }

    public void Draw(SpriteRenderer renderer)
    {
        if (!state.Detailed) return;
        float left = layout.Left, top = layout.Top;
        float usableWidth = layout.Width;
        renderer.Draw(pixel,
            new Rect(padding, padding, Math.Max(1, width - padding * 2), Math.Max(1, height - padding * 2)),
            new Vector4(.025f, .035f, .05f, .93f));
        // At tiny sizes, omit rows which cannot fit; wrapped body remains scrollable
        for (int i = 0; i < layout.Headers; i++)
            renderer.DrawText(headers[i], new(left, top + i * lineHeight), 1,
                i == 0 ? Colors.LightBlue.ToVector4() : Colors.WhiteSmoke.ToVector4());
        float chartTop = layout.ChartTop, chartHeight = layout.ChartHeight;
        if (chartHeight > 0)
        {
            renderer.Draw(pixel, new Rect(left, chartTop, usableWidth, chartHeight), new Vector4(.06f, .08f, .11f, 1));
            int bars = Math.Min(128, historyCount);
            for (int i = 0; i < bars; i++)
            {
                int first = i * historyCount / bars, end = (i + 1) * historyCount / bars;
                double value = 0;
                for (int j = first; j < end; j++) value = Math.Max(value, history[j].Milliseconds);
                float barHeight = (float)Math.Min(1, value / chartMaximum) * chartHeight;
                renderer.Draw(pixel,
                    new Rect(left + i * usableWidth / bars, chartTop + chartHeight - barHeight,
                        Math.Max(.5f, usableWidth / bars - 1), barHeight),
                    value > chartMaximum ? new Vector4(1, .35f, .3f, 1) : new Vector4(.35f, .75f, 1, 1));
            }

            renderer.Draw(pixel, new Rect(left, chartTop + chartHeight * (1 - 16.67f / chartMaximum), usableWidth, 1),
                new Vector4(.8f, .8f, .5f, .8f));
        }

        float bodyTop = layout.BodyTop;
        for (int i = 0; i < visibleRows; i++)
            renderer.DrawText(lines[i], new(left, bodyTop + i * lineHeight), 1, Colors.WhiteSmoke.ToVector4());
        if (height >= lineHeight + padding * 4)
            renderer.DrawText(footer, new(left, layout.FooterTop), 1, Colors.LightBlue.ToVector4());
    }

    public void Dispose()
    {
        foreach (var text in headers.Concat(lines).Append(footer).Append(measure))
        {
            text.Dispose();
        }
        
        pixel.Dispose();
    }
}

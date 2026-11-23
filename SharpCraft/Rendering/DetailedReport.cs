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

    // Display order; indentation marks scopes nested in the row above. "(all)" covers terrain and UI
    private static readonly (CpuMetric Metric, string Name)[] CpuRowOrder =
    [
        (CpuMetric.Work, "Main work"),
        (CpuMetric.Limiter, "Frame limiter"),
        (CpuMetric.World, "World tick"),
        (CpuMetric.Completions, "  Pipeline results"),
        (CpuMetric.Renderer, "Renderer incl. UI"),
        (CpuMetric.Culling, "  Culling"),
        (CpuMetric.Assembly, "  Transparent sort"),
        (CpuMetric.TerrainUpload, "  Terrain upload"),
        (CpuMetric.Recording, "  Scene commands"),
        (CpuMetric.Ui, "  UI prepare / upload"),
        (CpuMetric.Acquire, "  Swapchain acquire"),
        (CpuMetric.Submit, "  Submit (all)"),
        (CpuMetric.Staging, "  Upload staging (all)"),
        (CpuMetric.TransferBufferWait, "    Transfer buffer wait"),
        (CpuMetric.UploadCommands, "  Upload commands (all)")
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
    private static string N(long count) => CompactProfileText.Count((ulong)Math.Max(0, count));

    private static string Size(double bytes) => bytes switch
    {
        >= 1L << 30 => F($"{bytes / (1L << 30):0.00} GiB"),
        >= 1L << 20 => F($"{bytes / (1L << 20):0.0} MiB"),
        >= 1L << 10 => F($"{bytes / (1L << 10):0.0} KiB"),
        _ => F($"{bytes:0} B")
    };

    private static string Percent(long part, long whole) => whole == 0 ? "-" : F($"{part * 100.0 / whole:0}%");

    private void Rebuild()
    {
        var profile = profiler.Snapshot;
        var frame = profiler.FrameStatistics;
        headers[0].Set(font, Fit($"SharpCraft detailed report | {PageNames[state.Page]} | Shift+F3 close"));
        headers[1].Set(font,
            Fit(F(
                $"{(frame.Mean > 0 ? 1000 / frame.Mean : 0):0.0} FPS | frame {frame.Mean:0.00} ms | P95 {frame.P95:0.00} | max {frame.Maximum:0.00}")));
        headers[2].Set(font,
            Fit(F(
                $"{options.Scene?.ToString() ?? "Interactive"} | Radius {options.Radius} | CPU cap {(options.FpsLimit == 0 ? "off" : $"{options.FpsLimit} FPS")} / {device.PresentationMode} | XYZ {position.X:0.0}, {position.Y:0.0}, {position.Z:0.0}")));
        historyCount = profiler.CopyHistory(history);
        chartMaximum = (float)Math.Max(33.34, frame.P95 * 1.5);
        headers[3].Set(font,
            Fit(historyCount == 0
                ? "Waiting for frame samples..."
                : F($"Last {historyCount} frames | scale 0..{chartMaximum:0.0} ms | line 16.67 ms")));

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
        rows.Add("CPU ms / frame                  Mean      Min      P95      Max");
        foreach (var (metric, name) in CpuRowOrder)
        {
            var s = profiler.Statistics(metric);
            rows.Add(F($"{name,-27} {s.Mean,8:0.000} {s.Minimum,8:0.000} {s.P95,8:0.000} {s.Maximum,8:0.000}"));
        }

        var memory = profiler.Memory;
        rows.Add("");
        rows.Add(F(
            $"Memory   heap {Size(memory.ManagedBytes)} | private {Size(memory.ProcessBytes)} | main thread {Size(profiler.Snapshot.MainThreadAllocatedBytesPerFrame)}/frame"));
        rows.Add(F(
            $"GC       G0 {memory.Gen0Collections} | G1 {memory.Gen1Collections} | G2 {memory.Gen2Collections} | {Size(memory.TotalAllocatedBytes)} allocated since launch"));
    }

    private void StreamingRows(StreamingStatistics streaming)
    {
        rows.Add("Stage        Queue  Run/Wk   Done/s    Completed  Failed  Skipped");
        JobRow("Generation", streaming.GenerationQueued, streaming.Generation);
        JobRow("Lighting", streaming.LightingQueued, streaming.Lighting);
        JobRow("Meshing", streaming.MeshingQueued, streaming.Meshing);
        rows.Add("");
        rows.Add("Job ms         Mean      P95      Max  Wait P95   Oldest");
        JobDuration("Generation", streaming.Generation);
        JobDuration("Lighting", streaming.Lighting);
        JobDuration("Meshing", streaming.Meshing);
        rows.Add("");
        rows.Add(F(
            $"Main thread  results {streaming.ResultsQueued} | fresh {streaming.FreshBacklog} | deletions {streaming.DeletionBacklog} | stale {streaming.StaleResults:N0}"));
    }

    private void JobRow(string name, int queued, JobStatistics s) => rows.Add(
        F($"{name,-11} {queued,6} {s.Running + "/" + s.Workers,7} {s.PerSecond,8:0.0} {s.Completed,12:N0} {s.Failed,7} {s.Skipped,8}"));

    private void JobDuration(string name, JobStatistics s) => rows.Add(
        F($"{name,-11} {s.Duration.Mean,8:0.000} {s.Duration.P95,8:0.000} {s.Duration.Maximum,8:0.000} {s.QueueWait.P95,9:0.0} {s.OldestQueuedMilliseconds,8:0.0}"));

    private void RendererRows(FrameProfile profile)
    {
        var r = profile.Rendering;
        var g = profile.GpuResources;
        var t = profile.TerrainMemory;
        rows.Add($"Device      {device.DeviceName} ({device.DriverName})");
        rows.Add(F($"Chunks      {r.ResidentChunks:N0} resident | {r.VisibleChunks:N0} visible"));
        rows.Add(F($"Draws       {r.TerrainDrawCalls:N0} terrain | {r.UiDrawCalls} UI | {r.SkippedFrames} skipped frames"));
        rows.Add(F($"Faces       {N(r.SubmittedOpaqueFaces)} opaque | {N(r.SubmittedTransparentFaces)} transparent drawn"));
        rows.Add("");
        rows.Add(F(
            $"Uploads     {r.UploadJobs} jobs, {Size(r.TerrainUploadBytes + r.UiUploadBytes)} last frame | avg terrain {Size(profile.TerrainUploadBytesPerFrame)}, UI {Size(profile.UiUploadBytesPerFrame)}"));
        rows.Add(F($"Mesh queue  {t.QueuedMeshUpdates:N0} updates, {Size(t.QueuedMeshBytes)}"));
        rows.Add(F(
            $"Staging     peak {t.StagingBuffersInFlight}/{t.StagingBufferCount} in flight · {Size(t.StagingReservedBytes)} reserved | waits {profile.TransferBufferWaitsPerFrame:0.00}/frame, {profile.Timings.TransferBufferWaitMilliseconds:0.000} ms"));
        rows.Add(F($"Retiring    {t.RetiringAllocations:N0} allocations, {Size(t.RetiringBytes)}"));
        rows.Add("");
        rows.Add("Arena            Opaque                  Transparent");
        ArenaRow("Pages", t, a => F($"{a.Pages} · {Size(a.CapacityBytes)}"));
        ArenaRow("Slots", t, a => F($"{a.UsedSlots:N0} / {a.SlotCapacity:N0}"));
        ArenaRow("Faces / rounded", t, a => $"{N(a.RequestedFaces)} / {N(a.ReservedFaces)} · {Percent(a.RequestedFaces, a.ReservedFaces)}");
        ArenaRow("Free (largest)", t, a => $"{N(a.FreeFaces)} ({N(a.LargestFreeBlock)})");
        rows.Add("");
        rows.Add(F(
            $"GPU tracked {Size(g.TotalBytes)} | {g.BufferCount} buffers {Size(g.BufferBytes)} | {g.TextureCount} textures {Size(g.TextureBytes)}"));
    }

    private void ArenaRow(string name, TerrainMemoryStatistics t, Func<ArenaStatistics, string> value) =>
        rows.Add($"{name,-16} {value(t.Opaque),-23} {value(t.Transparent)}");

    private void BenchmarkRows()
    {
        const string cli = "CLI  --benchmark exposure|merge|terrain --seed N --radius N --path rotate|travel --fps 0";
        if (!options.Scene.HasValue)
        {
            rows.Add(F($"Interactive | radius {options.Radius}. Start a run from the command line:"));
            rows.Add(cli);
            return;
        }

        rows.Add(F(
            $"Scene       {options.Scene} | seed {options.Seed} | radius {options.Radius} | path {options.Path}"));
        rows.Add(F(
            $"Run         {benchmark.Phase} | {benchmark.RemainingSeconds:0.0} s left | {benchmark.Frames:N0} frames | warmup {options.WarmupSeconds:0.0} s, measure {options.MeasureSeconds:0.0} s"));
        rows.Add("");
        rows.Add(F(
            $"Frame ms    mean {benchmark.MeanMilliseconds:0.000} | P95 {benchmark.P95Milliseconds:0.0} | max {benchmark.MaximumMilliseconds:0.000}"));
        rows.Add(F($"Main work   {benchmark.MeanWorkMilliseconds:0.000} ms/frame | {benchmark.SkippedFrames} skipped frames"));
        rows.Add(F(
            $"Per frame   uploads {Size(benchmark.UploadBytesPerFrame)} | main-thread alloc {Size(benchmark.AllocatedBytesPerFrame)}"));
        rows.Add("");
        rows.Add(cli);
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

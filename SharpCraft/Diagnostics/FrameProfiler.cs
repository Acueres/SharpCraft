using System.Diagnostics;
using SharpCraft.Graphics;

namespace SharpCraft.Diagnostics;

internal sealed class FrameProfiler : IDisposable
{
    private const double RefreshMilliseconds = 250;
    private readonly FrameSample[] frameHistory = new FrameSample[256];
    private readonly SampleWindow frameDurations = new();
    private readonly SampleWindow[] cpuDurations = Enumerable.Range(0, (int)CpuMetric.Count).Select(_ => new SampleWindow()).ToArray();
    private readonly DurationStatistics[] cpuStatistics = new DurationStatistics[(int)CpuMetric.Count];
    private long frameId;
    private long memoryReadAt;
    private readonly Process process = Process.GetCurrentProcess();
    public MemoryStatistics Memory { get; private set; }
    public DurationStatistics FrameStatistics { get; private set; }
    public int HistoryCount => historyCount;
    public DurationStatistics Statistics(CpuMetric metric) => cpuStatistics[(int)metric];

    public int CopyHistory(Span<FrameSample> destination)
    {
        int count = Math.Min(destination.Length, historyCount);
        int first = (historyCursor - count + frameHistory.Length) % frameHistory.Length;
        for (int i = 0; i < count; i++) destination[i] = frameHistory[(first + i) % frameHistory.Length];
        return count;
    }
    private int historyCount;
    private int historyCursor;
    private long frameStarted;
    private long allocationStarted;
    private long revision;

    private CpuTimings totals;
    private int sampleCount;
    private double elapsedMilliseconds;
    private long terrainUploadBytes;
    private long uiUploadBytes;
    private long allocatedBytes;

    public CpuTimings Timings;
    public RenderStatistics Rendering;
    public StreamingStatistics Streaming;
    public FrameProfile Snapshot { get; private set; }
    public FrameSample LatestFrame { get; private set; }

    public void BeginFrame()
    {
        frameStarted = Stopwatch.GetTimestamp();
        allocationStarted = GC.GetAllocatedBytesForCurrentThread();
        Timings = default;
        // Geometry gauges persist when the world is unchanged. Activity never does
        Rendering.SubmittedOpaqueFaces = 0;
        Rendering.SubmittedTransparentFaces = 0;
        Rendering.UploadJobs = 0;
        Rendering.SkippedFrames = 0;
        Rendering.TerrainDrawCalls = 0;
        Rendering.UiDrawCalls = 0;
        Rendering.TerrainUploadBytes = 0;
        Rendering.UiUploadBytes = 0;
    }

    public void EndWork()
    {
        Timings.WorkMilliseconds = Stopwatch.GetElapsedTime(frameStarted).TotalMilliseconds;
    }

    public void EndFrame(Func<GpuResourceUsage> readGpuResources)
    {
        RecordFrame(Stopwatch.GetElapsedTime(frameStarted).TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocationStarted, readGpuResources);
    }

    // Kept independent of the clock so aggregation can be verified with known samples
    internal void RecordFrame(double frameMilliseconds, long mainThreadAllocatedBytes,
        Func<GpuResourceUsage> readGpuResources)
    {
        LatestFrame = new(++frameId, frameMilliseconds, Timings, Rendering, mainThreadAllocatedBytes);
        frameHistory[historyCursor] = LatestFrame;
        frameDurations.Add(frameMilliseconds);
        for (int i = 0; i < cpuDurations.Length; i++) cpuDurations[i].Add(Timings.Get((CpuMetric)i));
        historyCursor = (historyCursor + 1) % frameHistory.Length;
        historyCount = Math.Min(historyCount + 1, frameHistory.Length);

        sampleCount++;
        elapsedMilliseconds += frameMilliseconds;
        terrainUploadBytes += Rendering.TerrainUploadBytes;
        uiUploadBytes += Rendering.UiUploadBytes;
        allocatedBytes += mainThreadAllocatedBytes;
        totals.WorkMilliseconds += Timings.WorkMilliseconds;
        totals.LimiterMilliseconds += Timings.LimiterMilliseconds;
        totals.WorldMilliseconds += Timings.WorldMilliseconds;
        totals.RendererMilliseconds += Timings.RendererMilliseconds;
        totals.CullingMilliseconds += Timings.CullingMilliseconds;
        totals.AssemblyMilliseconds += Timings.AssemblyMilliseconds;
        totals.CompletionMilliseconds += Timings.CompletionMilliseconds;
        totals.UploadStagingMilliseconds += Timings.UploadStagingMilliseconds;
        totals.UploadCommandsMilliseconds += Timings.UploadCommandsMilliseconds;
        totals.CommandRecordingMilliseconds += Timings.CommandRecordingMilliseconds;
        totals.SubmitMilliseconds += Timings.SubmitMilliseconds;
        totals.AcquireMilliseconds += Timings.AcquireMilliseconds;
        totals.UiMilliseconds += Timings.UiMilliseconds;
        totals.TerrainUploadMilliseconds += Timings.TerrainUploadMilliseconds;

        if (elapsedMilliseconds < RefreshMilliseconds) return;

        FrameStatistics = frameDurations.Statistics();
        for (int i = 0; i < cpuDurations.Length; i++) cpuStatistics[i] = cpuDurations[i].Statistics();
        ReadMemory();
        double p95 = FrameStatistics.P95;
        double scale = 1.0 / sampleCount;

        Snapshot = new FrameProfile(
            ++revision,
            sampleCount * 1000.0 / elapsedMilliseconds,
            elapsedMilliseconds * scale,
            p95,
            new CpuTimings
            {
                WorkMilliseconds = totals.WorkMilliseconds * scale,
                LimiterMilliseconds = totals.LimiterMilliseconds * scale,
                WorldMilliseconds = totals.WorldMilliseconds * scale,
                RendererMilliseconds = totals.RendererMilliseconds * scale,
                CullingMilliseconds = totals.CullingMilliseconds * scale,
                AssemblyMilliseconds = totals.AssemblyMilliseconds * scale,
                TerrainUploadMilliseconds = totals.TerrainUploadMilliseconds * scale,
                CompletionMilliseconds = totals.CompletionMilliseconds * scale,
                UploadStagingMilliseconds = totals.UploadStagingMilliseconds * scale,
                UploadCommandsMilliseconds = totals.UploadCommandsMilliseconds * scale,
                CommandRecordingMilliseconds = totals.CommandRecordingMilliseconds * scale,
                SubmitMilliseconds = totals.SubmitMilliseconds * scale,
                AcquireMilliseconds = totals.AcquireMilliseconds * scale,
                UiMilliseconds = totals.UiMilliseconds * scale
            },
            Rendering,
            Streaming,
            terrainUploadBytes * scale,
            uiUploadBytes * scale,
            allocatedBytes * scale,
            readGpuResources());

        totals = default;
        sampleCount = 0;
        elapsedMilliseconds = 0;
        terrainUploadBytes = 0;
        uiUploadBytes = 0;
        allocatedBytes = 0;
    }
    private void ReadMemory()
    {
        long now = Stopwatch.GetTimestamp();
        if (memoryReadAt != 0 && Stopwatch.GetElapsedTime(memoryReadAt, now).TotalSeconds < 1) return;
        process.Refresh();
        long total = GC.GetTotalAllocatedBytes(false);
        Memory = new(GC.GetTotalMemory(false), process.PrivateMemorySize64,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), total);
        memoryReadAt = now;
    }

    public void Dispose() => process.Dispose();
}

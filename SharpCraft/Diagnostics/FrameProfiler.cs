using System.Diagnostics;

namespace SharpCraft.Diagnostics;

internal sealed class FrameProfiler
{
    private const double RefreshMilliseconds = 250;
    private readonly double[] frameHistory = new double[256];
    private readonly double[] percentileScratch = new double[256];
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

    public void BeginFrame()
    {
        frameStarted = Stopwatch.GetTimestamp();
        allocationStarted = GC.GetAllocatedBytesForCurrentThread();
        Timings = default;
        // Geometry gauges persist when the world is unchanged. Activity never does
        Rendering.TerrainDrawCalls = 0;
        Rendering.UiDrawCalls = 0;
        Rendering.TerrainUploadBytes = 0;
        Rendering.UiUploadBytes = 0;
    }

    public void EndWork()
    {
        Timings.WorkMilliseconds = Stopwatch.GetElapsedTime(frameStarted).TotalMilliseconds;
    }

    public void EndFrame()
    {
        RecordFrame(Stopwatch.GetElapsedTime(frameStarted).TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocationStarted);
    }

    // Kept independent of the clock so aggregation can be verified with known samples
    private void RecordFrame(double frameMilliseconds, long mainThreadAllocatedBytes)
    {
        frameHistory[historyCursor] = frameMilliseconds;
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
        totals.TerrainUploadMilliseconds += Timings.TerrainUploadMilliseconds;

        if (elapsedMilliseconds < RefreshMilliseconds) return;

        Array.Copy(frameHistory, percentileScratch, historyCount);
        Array.Sort(percentileScratch, 0, historyCount);
        double p95 = percentileScratch[(int)Math.Ceiling(historyCount * 0.95) - 1];
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
                TerrainUploadMilliseconds = totals.TerrainUploadMilliseconds * scale
            },
            Rendering,
            Streaming,
            terrainUploadBytes * scale,
            uiUploadBytes * scale,
            allocatedBytes * scale);

        totals = default;
        sampleCount = 0;
        elapsedMilliseconds = 0;
        terrainUploadBytes = 0;
        uiUploadBytes = 0;
        allocatedBytes = 0;
    }
}

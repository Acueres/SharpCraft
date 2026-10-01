namespace SharpCraft.Diagnostics;

internal sealed class BenchmarkRun(BenchmarkOptions options)
{
    // 0.1ms histogram bins, with an overflow bin. Bounded even during uncapped runs.
    private readonly long[] histogram = new long[10001];
    private double elapsed;
    private double milliseconds;
    private double work;
    private double maximum;
    private long uploads;
    private long allocations;
    private long frames;
    private long skipped;
    public string Phase => !options.Scene.HasValue ? "Interactive" :
        elapsed < options.WarmupSeconds ? "Warmup" : Complete ? "Complete" : "Measuring";
    public bool Complete => elapsed >= options.WarmupSeconds + options.MeasureSeconds;
    public double RouteSeconds => Math.Min(elapsed, options.WarmupSeconds + options.MeasureSeconds);
    public long Frames => frames;
    public double RemainingSeconds => Math.Max(0, options.WarmupSeconds + options.MeasureSeconds - elapsed);
    public double MeanMilliseconds => frames == 0 ? 0 : milliseconds / frames;
    public double MeanWorkMilliseconds => frames == 0 ? 0 : work / frames;
    public double MaximumMilliseconds => maximum;
    public double UploadBytesPerFrame => frames == 0 ? 0 : uploads / (double)frames;
    public double AllocatedBytesPerFrame => frames == 0 ? 0 : allocations / (double)frames;
    public long SkippedFrames => skipped;
    public double P95Milliseconds
    {
        get
        {
            if (frames == 0) return 0;
            long threshold = (long)Math.Ceiling(frames * .95), count = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                count += histogram[i];
                if (count >= threshold) return i == histogram.Length - 1 ? double.PositiveInfinity : (i + 1) / 10.0;
            }
            return 0;
        }
    }

    public void Record(in FrameSample sample)
    {
        if (!options.Scene.HasValue || Complete) return;
        // Classify by frame start; no partial-frame weighting at either boundary.
        if (elapsed >= options.WarmupSeconds)
        {
            frames++;
            milliseconds += sample.Milliseconds;
            work += sample.Timings.WorkMilliseconds;
            maximum = Math.Max(maximum, sample.Milliseconds);
            uploads += sample.Rendering.TerrainUploadBytes + sample.Rendering.UiUploadBytes;
            allocations += sample.AllocatedBytes;
            skipped += sample.Rendering.SkippedFrames;
            histogram[Math.Min((int)(sample.Milliseconds * 10), histogram.Length - 1)]++;
        }
        elapsed += sample.Milliseconds / 1000;
    }
}

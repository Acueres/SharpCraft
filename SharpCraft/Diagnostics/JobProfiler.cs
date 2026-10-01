using System.Diagnostics;

namespace SharpCraft.Diagnostics;

internal readonly record struct JobStatistics(int Workers, int Running, long Completed, long Failed,
    long Skipped, double PerSecond, double OldestQueuedMilliseconds,
    DurationStatistics Duration, DurationStatistics QueueWait);

internal sealed class JobProfiler
{
    private readonly Lock gate = new();
    private readonly SampleWindow duration = new(128);
    private readonly SampleWindow wait = new(128);
    private int running;
    private long completed;
    private long failed;
    private long skipped;
    private long lastRefresh = Stopwatch.GetTimestamp();
    private long previousCompleted;
    private DurationStatistics durationSnapshot;
    private DurationStatistics waitSnapshot;
    private double rate;
    public int Workers { get; set; }

    public JobMeasurement Start(long queuedAt)
    {
        long started = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref running);
        lock (gate) wait.Add(Stopwatch.GetElapsedTime(queuedAt, started).TotalMilliseconds);
        return new(this, started);
    }

    // Outcomes count executed work, including results later rejected as stale.
    public void Outcome(bool success, bool failure)
    {
        if (success) Interlocked.Increment(ref completed);
        else if (failure) Interlocked.Increment(ref failed);
        else Interlocked.Increment(ref skipped);
    }

    private void Finish(long started)
    {
        double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        lock (gate) duration.Add(elapsed);
        Interlocked.Decrement(ref running);
    }

    public JobStatistics Snapshot(double oldestQueuedMilliseconds)
    {
        long now = Stopwatch.GetTimestamp();
        long done = Interlocked.Read(ref completed);
        double elapsed = Stopwatch.GetElapsedTime(lastRefresh, now).TotalMilliseconds;
        if (elapsed >= 250)
        {
            lock (gate)
            {
                durationSnapshot = duration.Statistics();
                waitSnapshot = wait.Statistics();
            }
            rate = (done - previousCompleted) * 1000.0 / elapsed;
            previousCompleted = done;
            lastRefresh = now;
        }
        return new(Workers, Volatile.Read(ref running), done, Interlocked.Read(ref failed),
            Interlocked.Read(ref skipped), rate, oldestQueuedMilliseconds, durationSnapshot, waitSnapshot);
    }

    internal readonly struct JobMeasurement(JobProfiler owner, long started) : IDisposable
    {
        public void Dispose() => owner.Finish(started);
    }
}

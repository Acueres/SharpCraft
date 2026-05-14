using System.Diagnostics;

namespace SharpCraft.Time;

internal class FrameClock
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private double previousSeconds;

    public FrameTime Tick()
    {
        double currentSeconds = stopwatch.Elapsed.TotalSeconds;
        double deltaSeconds = currentSeconds - previousSeconds;
        previousSeconds = currentSeconds;

        const double MaxDeltaSeconds = 0.1; // 100 ms
        deltaSeconds = Math.Min(deltaSeconds, MaxDeltaSeconds);

        return new FrameTime(
            TotalSeconds: currentSeconds,
            DeltaSeconds: (float)deltaSeconds
        );
    }
}

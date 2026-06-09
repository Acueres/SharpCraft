using System.Diagnostics;

namespace SharpCraft.Time;

internal class FrameClock
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private double previousSeconds;

    private int sampleFrames;
    private double sampleElapsed;
    private int currentFps;

    public FrameTime Tick()
    {
        const double FpsSampleInterval = 0.25;

        double currentSeconds = stopwatch.Elapsed.TotalSeconds;
        double deltaSeconds = currentSeconds - previousSeconds;
        previousSeconds = currentSeconds;

        const double MaxDeltaSeconds = 0.1; // 100 ms
        double clampedDeltaSeconds = Math.Min(deltaSeconds, MaxDeltaSeconds);

        sampleFrames++;
        sampleElapsed += deltaSeconds;

        if (sampleElapsed >= FpsSampleInterval)
        {
            currentFps = (int)Math.Round(sampleFrames / sampleElapsed);
            sampleElapsed = 0;
            sampleFrames = 0;
        }

        return new FrameTime(
            TotalSeconds: currentSeconds,
            DeltaSeconds: (float)clampedDeltaSeconds,
            Fps: currentFps
        );
    }
}

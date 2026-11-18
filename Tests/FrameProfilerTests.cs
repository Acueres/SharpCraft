using SharpCraft.Diagnostics;

namespace Tests;

public class FrameProfilerTests
{
    [Fact]
    public void TransferBufferWaitAveragesIncludeFramesWithoutWaitsAndResetAfterRefresh()
    {
        using var profiler = new FrameProfiler();

        profiler.BeginFrame();
        profiler.Timings.UploadStagingMilliseconds = 9;
        profiler.Timings.TransferBufferWaitMilliseconds = 6;
        profiler.Rendering.TransferBufferWaits = 2;
        profiler.RecordFrame(125, 0, () => default);

        profiler.BeginFrame();
        profiler.Timings.UploadStagingMilliseconds = 1;
        profiler.RecordFrame(125, 0, () => default);

        Assert.Equal(3.0, profiler.Snapshot.Timings.TransferBufferWaitMilliseconds);
        Assert.Equal(1.0, profiler.Snapshot.TransferBufferWaitsPerFrame);
        Assert.Equal(5.0, profiler.Snapshot.Timings.UploadStagingMilliseconds);
        Assert.Equal(0, profiler.LatestFrame.Rendering.TransferBufferWaits);
        Assert.Equal(3.0, profiler.Statistics(CpuMetric.TransferBufferWait).Mean);
        Assert.Equal(6.0, profiler.Statistics(CpuMetric.TransferBufferWait).Maximum);

        profiler.BeginFrame();
        profiler.RecordFrame(250, 0, () => default);

        Assert.Equal(0.0, profiler.Snapshot.Timings.TransferBufferWaitMilliseconds);
        Assert.Equal(0.0, profiler.Snapshot.TransferBufferWaitsPerFrame);
    }

    [Fact]
    public void FrameHistoryKeepsTransferBufferWaitTimeAndCount()
    {
        using var profiler = new FrameProfiler();

        profiler.BeginFrame();
        profiler.Timings.TransferBufferWaitMilliseconds = 2.5;
        profiler.Rendering.TransferBufferWaits = 3;
        profiler.RecordFrame(16, 0, () => default);

        profiler.BeginFrame();
        profiler.RecordFrame(16, 0, () => default);

        FrameSample[] history = new FrameSample[2];
        Assert.Equal(2, profiler.CopyHistory(history));
        Assert.Equal(2.5, history[0].Timings.TransferBufferWaitMilliseconds);
        Assert.Equal(3, history[0].Rendering.TransferBufferWaits);
        Assert.Equal(0.0, history[1].Timings.TransferBufferWaitMilliseconds);
        Assert.Equal(0, history[1].Rendering.TransferBufferWaits);
    }
}

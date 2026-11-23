using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering;

using System.Runtime.CompilerServices;

namespace SharpCraft.Diagnostics;

// Main-thread wall-clock durations. Renderer includes its terrain-update subtimings;
// these are not additive, and may include waits inside graphics API calls
internal struct CpuTimings
{
    public double WorkMilliseconds;
    public double LimiterMilliseconds;
    public double WorldMilliseconds;
    public double RendererMilliseconds;
    public double CullingMilliseconds;
    public double AssemblyMilliseconds;
    public double TerrainUploadMilliseconds;
    public double CompletionMilliseconds;
    public double UploadStagingMilliseconds;
    public double TransferBufferWaitMilliseconds;
    public double UploadCommandsMilliseconds;
    public double CommandRecordingMilliseconds;
    public double SubmitMilliseconds;
    public double AcquireMilliseconds;
    public double UiMilliseconds;

    public readonly double Get(CpuMetric metric) => metric switch
    {
        CpuMetric.Work => WorkMilliseconds,
        CpuMetric.Limiter => LimiterMilliseconds,
        CpuMetric.World => WorldMilliseconds,
        CpuMetric.Completions => CompletionMilliseconds,
        CpuMetric.Renderer => RendererMilliseconds,
        CpuMetric.Culling => CullingMilliseconds,
        CpuMetric.Assembly => AssemblyMilliseconds,
        CpuMetric.TerrainUpload => TerrainUploadMilliseconds,
        CpuMetric.Staging => UploadStagingMilliseconds,
        CpuMetric.TransferBufferWait => TransferBufferWaitMilliseconds,
        CpuMetric.UploadCommands => UploadCommandsMilliseconds,
        CpuMetric.Recording => CommandRecordingMilliseconds,
        CpuMetric.Submit => SubmitMilliseconds,
        CpuMetric.Acquire => AcquireMilliseconds,
        CpuMetric.Ui => UiMilliseconds,
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };
}

internal enum CpuMetric
{
    Work, Limiter, World, Completions, Renderer, Culling, Assembly, TerrainUpload,
    Staging, TransferBufferWait, UploadCommands, Recording, Submit, Acquire, Ui, Count
}

internal struct RenderStatistics
{
    public int ResidentChunks;
    public int VisibleChunks;
    public uint OpaqueFaces;
    public uint TransparentFaces;
    public int TerrainDrawCalls;
    public int UiDrawCalls;
    public long TerrainUploadBytes;
    public long UiUploadBytes;
    public uint SubmittedOpaqueFaces;
    public uint SubmittedTransparentFaces;
    public int UploadJobs;
    public int TransferBufferWaits;
    public int SkippedFrames;
}

internal readonly record struct StreamingStatistics(int GenerationQueued, int LightingQueued, int MeshingQueued)
{
    public JobStatistics Generation { get; init; }
    public JobStatistics Lighting { get; init; }
    public JobStatistics Meshing { get; init; }
    public int ResultsQueued { get; init; }
    public long StaleResults { get; init; }
    // Main-thread queues throttled by the pipeline tick budget
    public int FreshBacklog { get; init; }
    public int DeletionBacklog { get; init; }
}

// Renderer-side terrain memory gauges. Snapshots replace StagingBuffersInFlight with the interval peak
internal readonly record struct TerrainMemoryStatistics(
    ArenaStatistics Opaque,
    ArenaStatistics Transparent,
    int RetiringAllocations,
    long RetiringBytes,
    int QueuedMeshUpdates,
    long QueuedMeshBytes,
    int StagingBuffersInFlight,
    int StagingBufferCount,
    long StagingReservedBytes)
{
    public long CapacityBytes => Opaque.CapacityBytes + Transparent.CapacityBytes;
    public long RequestedBytes =>
        (Opaque.RequestedFaces + Transparent.RequestedFaces) * Unsafe.SizeOf<VoxelFace>();
}

internal readonly record struct DurationStatistics(int Count, double Mean, double Minimum, double P95, double Maximum);
internal readonly record struct FrameSample(long Id, double Milliseconds, CpuTimings Timings,
    RenderStatistics Rendering, long AllocatedBytes);
internal readonly record struct MemoryStatistics(long ManagedBytes, long ProcessBytes,
    int Gen0Collections, int Gen1Collections, int Gen2Collections, long TotalAllocatedBytes);

internal readonly record struct FrameProfile(
    long Revision,
    double Fps,
    double FrameMilliseconds,
    double P95FrameMilliseconds,
    CpuTimings Timings,
    RenderStatistics Rendering,
    StreamingStatistics Streaming,
    double TerrainUploadBytesPerFrame,
    double UiUploadBytesPerFrame,
    double TransferBufferWaitsPerFrame,
    double MainThreadAllocatedBytesPerFrame,
    GpuResourceUsage GpuResources,
    TerrainMemoryStatistics TerrainMemory);

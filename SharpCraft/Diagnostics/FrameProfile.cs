using SharpCraft.Graphics;

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
    public long TerrainUsedBytes;
    // Explicit face-buffer capacity, not total driver/VRAM consumption
    public long TerrainBufferBytes;
}

internal readonly record struct StreamingStatistics(int GenerationQueued, int LightingQueued, int MeshingQueued);

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
    double MainThreadAllocatedBytesPerFrame,
    GpuResourceUsage GpuResources);

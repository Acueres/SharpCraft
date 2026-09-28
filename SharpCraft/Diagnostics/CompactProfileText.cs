namespace SharpCraft.Diagnostics;

internal static class CompactProfileText
{
    public static string Format(in FrameProfile profile, double managedMiB, double processMiB,
        int gen0, int gen1, int gen2)
    {
        var timings = profile.Timings;
        var rendering = profile.Rendering;
        var streaming = profile.Streaming;

        return FormattableString.Invariant($"""
            SharpCraft profiler | F3 hide | averages ~250 ms
            {profile.Fps:0} FPS | frame {profile.FrameMilliseconds:0.00} ms | recent P95 {profile.P95FrameMilliseconds:0.00} ms
            Main work {timings.WorkMilliseconds:0.00} ms | limiter {timings.LimiterMilliseconds:0.00} ms
            CPU world {timings.WorldMilliseconds:0.00} ms | renderer {timings.RendererMilliseconds:0.00} ms
              Terrain: cull {timings.CullingMilliseconds:0.00} | assemble {timings.AssemblyMilliseconds:0.00} | upload {timings.TerrainUploadMilliseconds:0.00} ms
            GPU time unavailable | draws: terrain {rendering.TerrainDrawCalls} / UI {rendering.UiDrawCalls}
            Chunks: visible {rendering.VisibleChunks:N0} / resident {rendering.ResidentChunks:N0}
            Faces: opaque {rendering.OpaqueFaces:N0} / transparent {rendering.TransparentFaces:N0}
            Uploads/frame avg: terrain {profile.TerrainUploadBytesPerFrame / 1024:0.0} / UI {profile.UiUploadBytesPerFrame / 1024:0.0} KiB
            Terrain buffers: used {rendering.TerrainUsedBytes / 1048576.0:0.0} / reserved {rendering.TerrainBufferBytes / 1048576.0:0.0} MiB
            Queued jobs: gen {streaming.GenerationQueued} / light {streaming.LightingQueued} / mesh {streaming.MeshingQueued}
            Alloc/main frame {profile.MainThreadAllocatedBytesPerFrame / 1024:0.0} KiB | GC {gen0}/{gen1}/{gen2}
            Memory: managed {managedMiB:0.0} / process {processMiB:0.0} MiB
            """);
    }
}

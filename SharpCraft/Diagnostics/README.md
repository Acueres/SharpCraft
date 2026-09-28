# Compact profiler

The overlay is visible at startup. **F3** toggles it; sampling continues while it
is hidden. Values refresh after approximately 250 ms of completed frames. Memory
and GC gauges refresh once per second.

- **Frame / FPS:** measured loop duration including the frame limiter. Simulation
  delta clamping does not affect profiling. Startup world generation is excluded.
- **Recent P95:** nearest-rank percentile of the last 256 loop durations (fewer
  during warm-up). This is a frame-count window, not a fixed number of seconds.
- **Main work:** main-thread wall time before the limiter, including UI refresh,
  graphics API calls, and any waits inside those calls. It is not CPU utilization.
- **World:** recentering, streaming tick, and queue-depth sampling.
- **Renderer:** terrain updates plus UI update and render/submission. Cull,
  assembly, and terrain upload are included subtimings; do not add them again.
- **GPU time:** unavailable. CPU submission time is never presented as GPU time.
- **Draws:** actual terrain and UI draw calls in the latest sampled frame.
- **Chunks:** frustum-surviving nonempty ready chunks versus all loaded chunks,
  including empty chunks. Geometry gauges persist between world/camera updates.
- **Faces:** face instances in the current opaque and transparent buffers.
- **Uploads/frame:** interval averages, including frames with zero uploads.
  Terrain counts successful face-buffer uploads; UI counts sprite geometry and
  standalone texture uploads. SDL_ttf uploads glyph atlas regions internally;
  these atlas uploads are not included. Startup assets are excluded.
- **Terrain buffers:** used face bytes and explicitly reserved buffer capacity.
  This excludes textures, staging buffers, and SDL's internal cycled resources;
  it is not a total VRAM measurement.
- **Queued jobs:** pending and channel-queued generation/lighting/meshing jobs.
  Counts are approximate during concurrent consumption and exclude executing jobs.
- **Alloc/main frame:** average bytes allocated on the main thread per loop.
  Worker allocations are excluded. GC counts are process-wide totals since start.
- **Memory:** managed live heap estimate and process private memory, both in MiB.

`FrameProfiler` collects numeric samples independently of text rendering. Its
fixed history and reusable percentile scratch buffer keep sampling bounded.
`FrameProfile` is a completed snapshot for this overlay and future report/logging
consumers. The display uses a persistent SDL_ttf text object backed by GPU glyph
atlases. Content changes rebuild layout; unchanged frames reuse SDL_ttf's cached
geometry. Glyphs draw through the sprite batch with linear filtering, and sprite
geometry is still uploaded each visible frame. Text preparation and any internal
atlas uploads happen before the render pass; their time remains in frame totals.

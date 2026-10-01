# Rendering diagnostics

The overlay is visible at startup and **F3** toggles it. Three text-only panels
show a quick overview of the view, world/CPU, and GPU submission/resources. On a
narrow window they reflow into two columns or a vertical stack. A small text
shadow keeps the glyphs legible without a panel background.

The **View** panel shows FPS, average frame duration, camera XYZ, and the camera's
chunk index. Coordinates refresh about every 100 ms. FPS and frame duration come
from completed profiling intervals of about 250 ms. Frame duration includes the
frame limiter; simulation delta clamping does not change that measurement.

The **World / CPU** panel shows loaded and visible chunk counts, queued generation
(G), lighting (L), and meshing (M) jobs, main-thread work time, managed heap, and
process private memory. Work time is main-thread wall time before the limiter;
it is not CPU utilization and can include graphics API waits. Queued counts are
approximate during concurrent consumption and exclude jobs already executing.
The memory values refresh about once per second and are shown in MiB.

The **GPU** panel shows the graphics device, tracked persistent resource bytes,
total opaque and transparent face instances, used/reserved terrain face-buffer
capacity, and average terrain uploads per frame. Tracked bytes sum the requested
sizes of live SharpCraft-created GPU buffers and textures, including both the
individual block textures and their texture array. The total excludes temporary
transfer buffers, SDL_ttf's glyph atlas, swapchain images, pipelines, samplers,
and driver overhead. It is not physical VRAM use. Terrain buffer capacity is a
subset of that total; its used value is the face payload size. Upload averages
include zero-upload frames. GPU execution time is not measured or shown. The
device name is shortened when needed to keep the panel compact.

`FrameProfiler` collects detailed CPU timings, rolling frame statistics,
allocation, and upload data for the expanded report. Fixed histories and reusable
percentile scratch buffers keep sampling bounded. The overlay uses
persistent SDL_ttf text layouts backed by the GPU glyph atlas. Only changed
values rebuild text geometry; sprite geometry is uploaded each visible frame.
SDL_ttf uploads atlas regions internally and those bytes are not counted by the
UI upload counter. Text preparation and atlas uploads occur before the render
pass and remain part of overall frame time.


## Detailed report

**Shift+F3** opens/closes the report without toggling the compact overlay preference.
The compact view is suppressed while the report is open. **1–4** select pages;
**Tab / Left / Right** cycle pages. **Up / Down**, **PageUp / PageDown**, and **Home**
scroll the current page. Text wraps on narrower windows; the chart is omitted
when height is insufficient. The report does not pause the simulation.

Every page shares frame mean, P95, maximum, camera/context, and the last 256
frames as a graph. Up to 128 bars show maxima of neighboring samples, preserving
spikes; a 16.67 ms reference line gives a 60 FPS target. CPU statistics use the
last 256 frames and refresh at roughly 250 ms intervals. Compact means and
upload/allocation averages retain their 250 ms interval semantics. No GPU query,
readback, forced collection, or per-frame memory/driver inspection is added.

1. **Frame / CPU**: mean/min/P95/max wall time for main work, limiter, world,
   completion handling, renderer, culling, face assembly, terrain upload,
   upload staging, upload commands, scene commands, submission, swapchain
   acquisition, and UI preparation/upload. Nested scopes overlap and cannot be
   summed. A zero in an intermittently executed scope includes idle frames.
   Managed heap, process private bytes, cumulative GC collection counts and
   approximate cumulative all-thread allocations refresh once a second.
2. **Streaming**: approximate queued/running counts, workers, successful jobs/sec,
   cumulative success/failure/skip counts, result backlog, rejected stale results,
   queue age, and mean/P95/max duration/wait. Last 128 finished jobs form service
   statistics; last 128 started jobs form queue-wait statistics. Job service time
   includes publishing into result/relight channels and its backpressure. Startup
   bulk generation is excluded. Worker timing is not part of CPU frame budget.
3. **Renderer / resources**: resident/visible chunks, face-list sizes, actual faces
   submitted (zero on skipped render frames), terrain/UI draws, submitted upload
   jobs and bytes, persistent tracked buffers/textures, and whole face-buffer
   requested/reserved bytes and capacity slack. Activity counters show the latest
   completed refresh frame; upload averages cover the interval. SDL_ttf atlas
   uploads remain internal, so upload and resource totals exclude them.
4. **Benchmarks**: deterministic scene/seed/radius/path, warmup and measurement
   state, and the completed run's mean/P95/max frame duration, main work,
   upload/allocation averages and skipped frames. The summary stays visible.

The installed SDL GPU binding does **not** expose timestamp queries. GPU durations
are explicitly unavailable; CPU command time is never relabeled as GPU time.
`GpuDevice.BeginGpuPass/EndGpuPass` provide opaque/transparent/UI boundaries using
SDL debug groups for external captures and future asynchronous timestamp hooks.
Full Phase 0 GPU timestamps still require backend/API support. Advanced pipeline
statistics, vendor utilization/memory queries and profiler integrations are deferred.

Indirect commands/list construction (Phase 3) and slot/page/arena fragmentation
and deferred-free counters (Phase 2) are explicitly **N/A**. The current renderer
has face buffers and immediate upload submissions, so it has no upload queue or
paged allocator to inspect.

## Reproducible Phase 0 worlds

An argument-free launch uses `CreateStartupOptions()` near the top of `App.cs`.
Change its `scene` assignment to switch modes:

```csharp
BenchmarkScene? scene = null;                   // Normal mode
BenchmarkScene? scene = BenchmarkScene.Terrain; // Seeded terrain benchmark
```

Use one assignment at a time. `Exposure` and `Merge` select the other presets.
The adjacent configuration block controls seed, chunk radius, camera path,
warmup/measurement duration and FPS limit. Normal mode uses `new BenchmarkOptions()`
(8-chunk radius and 60 FPS); those normal settings can also be edited there.
`BenchmarkOptions.ForScene(...)` supplies benchmark defaults (radius 4, no CPU
limiter) and supports `with` overrides. Both code and CLI configurations are
validated before native resources are created.

Explicit command-line arguments replace the entire in-code launch configuration;
they do not merge with it. For example, `--fps 60` selects normal mode even if the
code selects a benchmark. `--help` shows usage without starting the app.
Pass options after `--` with `dotnet run`:

```sh
dotnet run --project SharpCraft/SharpCraft.csproj -c Release -- --benchmark exposure --seed 1337 --radius 4 --path rotate --fps 0
dotnet run --project SharpCraft/SharpCraft.csproj -c Release -- --benchmark merge --seed 1337 --radius 4 --path rotate --fps 0
dotnet run --project SharpCraft/SharpCraft.csproj -c Release -- --benchmark terrain --seed 1337 --radius 4 --path travel --fps 0
```

- **Exposure**: bounded-height 3D checkerboard; neighboring solids expose their
  faces, making merging ineffective.
- **Merge**: plateaus, flat walls and straight tunnels.
- **Terrain**: seeded smooth hills, solid ground and connected caves using the
  existing sandstone material and lighting. This is the current worldgen baseline,
  not the future biome/vegetation/detail workloads.

All use integer world coordinates and a seed, independently of job ordering and
chunk boundaries. Options: `--seed` (signed integer), `--radius` (1..16 chunks),
`--path stationary|rotate|travel`, `--warmup` (default 5 seconds), `--seconds`
(default 20 seconds), and `--fps` (0..1000). Benchmark defaults are radius 4,
rotation and no CPU limiter. `--fps 0` requests immediate presentation when the
window/device supports it; otherwise VSync remains and is labeled in the report.
Radius and FPS can also be set for normal interactive startup.

Camera begins at (0,40,48). Rotation changes heading at 0.25 radians/sec;
travel moves +X at 8 blocks/sec. Camera pose is a function of measured run time.
Initial bulk loading and the initial renderer upload are excluded. Warmup advances
the same route; measurement includes whole frames whose start falls within the
measurement interval. Camera and summary freeze when measurement finishes; the
app stays open so the report can be read. Escape exits. Restart to repeat.

Benchmark frame P95 uses bounded 0.1 ms histogram bins (upper bin edge); P95 at/above
1000 ms is shown as Infinity. Other report P95 values are exact nearest-rank
percentiles of their bounded histories. UI drawing is included in measured work;
sample aggregation and benchmark bookkeeping after the frame are excluded.
Keep commit/build, seed/radius/path, warmup/duration, viewport, UI visibility,
presentation mode, and frame cap identical for comparisons. Test terrain at
radii 2, 4 and 8. Shader/build time is outside measurement.

Logging/export and advanced GPU instruments are deferred.

## Verification

Build the main solution and launch the main app for UI checks and benchmarks:

```sh
dotnet build SharpCraft.slnx
dotnet run --project SharpCraft/SharpCraft.csproj
```

The argument-free command uses the configuration inside `App`. Use the main-app
benchmark commands above when explicit launch options are more convenient. Check
F3 / Shift+F3, pages 1–4, scrolling and window resizing in the running app. Let a
benchmark finish to inspect its summary, then Escape to exit. No test project or
UI/benchmark test harness is involved.

# Compact profiler

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

The **GPU** panel shows the graphics device, terrain/UI draw calls, total opaque
and transparent face instances, used/reserved terrain face-buffer capacity, and
average terrain uploads per frame. Draw calls are from the latest sampled frame;
uploads include zero-upload frames. Terrain buffer capacity excludes textures,
staging buffers, and SDL's internal resources, so it is not a total VRAM figure.
GPU execution time is not measured or shown. The device name is shortened when
needed to keep the panel compact.

`FrameProfiler` still collects the more detailed timings, P95 frame time,
allocation, and upload data for a future expanded report. Its fixed history and
reusable percentile scratch buffer keep sampling bounded. The overlay uses
persistent SDL_ttf text layouts backed by the GPU glyph atlas. Only changed
values rebuild text geometry; sprite geometry is uploaded each visible frame.
SDL_ttf uploads atlas regions internally and those bytes are not counted by the
UI upload counter. Text preparation and atlas uploads occur before the render
pass and remain part of overall frame time.

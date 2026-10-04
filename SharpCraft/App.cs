using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Diagnostics;
using SharpCraft.Input;
using SharpCraft.Platform;
using SharpCraft.Rendering;
using SharpCraft.Rendering.Text;
using SharpCraft.Time;
using SharpCraft.World.Blocks;
using SharpCraft.SharpMath;
using SharpCraft.World.Generation;
using SharpCraft.World.Meshing;
using SharpCraft.World.WorldStreaming;
using SharpCraft.View;

using SDL;
using System.Numerics;
using System.Diagnostics;
using static SDL.SDL3;

namespace SharpCraft;

internal unsafe class App : IDisposable
{
    private const uint DefaultWidth = 1280;
    private const uint DefaultHeight = 720;

    private static BenchmarkOptions CreateStartupOptions()
    {
        // null = normal mode. Select Exposure, Merge or Terrain to run a benchmark.
        BenchmarkScene? scene = null;

        return scene.HasValue
            ? BenchmarkOptions.ForScene(scene.Value) with
            {
                Seed = 1337,
                Radius = 4,
                Path = BenchmarkPath.Rotate,
                WarmupSeconds = 5,
                MeasureSeconds = 20,
                FpsLimit = 0
            }
            : new BenchmarkOptions();
    }

    private readonly AssetServer assetServer;

    private readonly Renderer renderer;
    private readonly SdlRuntime sdlRuntime;
    private readonly Window window;
    private readonly GpuDevice device;
    private readonly FontSystem fontSystem;

    private readonly InputHandler input;
    
    private readonly Camera camera;
    private ICameraController activeViewController;
    
    private readonly FrameClock clock = new();
    private readonly FrameProfiler profiler = new();
    private readonly Func<GpuResourceUsage> readGpuResources;
    private readonly FrameLimiter? frameLimiter;
    private readonly BenchmarkRun benchmark;

    private readonly WorldLoader worldLoader;
    private readonly ChunkVolume volume;

    public App(BenchmarkOptions? options = null)
    {
        options ??= CreateStartupOptions();
        options.Validate();
        benchmark = new BenchmarkRun(options);
        sdlRuntime = new SdlRuntime();
        window = new Window("SharpCraft", (int)DefaultWidth, (int)DefaultHeight);
        device = new GpuDevice("vulkan", window, debugInfo: true, preferImmediate: options.FpsLimit == 0);
        readGpuResources = () => device.ResourceUsage;
        fontSystem = new FontSystem();

        assetServer = new AssetServer(device);
        var blockRegistry = new BlockRegistry(assetServer);

        input = new InputHandler();

        var initialViewpoint = Viewpoint.LookAt(
            index: new Vec3<int>(0, 2, 0),
            localPosition: Vector3.Zero,
            targetIndex: Vec3<int>.Zero,
            targetLocalPosition: new Vector3(0f, 8f, 4f),
            up: MathUtilities.Vector3Up
        );

        if (options.Scene.HasValue) initialViewpoint = BenchmarkCameraController.Pose(options.Path, 0);
        /*activeViewController = options.Scene.HasValue
            ? new BenchmarkCameraController(options, benchmark)
            : new OrbitViewController(
            target: Vector3.Zero,
            initialViewpoint: initialViewpoint,
            minimumDistance: 2f,
            maximumDistance: 500f
        );*/
        activeViewController = new ObserverViewController(initialViewpoint);

        camera = new Camera(initialViewpoint, DefaultWidth, DefaultHeight);

        frameLimiter = options.FpsLimit == 0 ? null : new FrameLimiter(options.FpsLimit);

        volume = new ChunkVolume(options.Radius);
        var chunkGenerator = new ChunkGenerator(blockRegistry,
            options.Scene.HasValue ? new BenchmarkTerrain(options.Scene.Value, options.Seed) : null);
        var chunkMesher = new ChunkMesher(blockRegistry);
        worldLoader = new WorldLoader(initialViewpoint.Index, volume, chunkGenerator, chunkMesher);
        worldLoader.BulkGenerate();

        renderer = new Renderer(DefaultWidth, DefaultHeight, window, device, assetServer, chunkMesher, profiler,
            options, benchmark);
        renderer.LoadGpuResources();
        renderer.UpdateWorld(camera, volume);
    }

    public void Run()
    {
        bool running = true;
        
        while (running)
        {
            profiler.BeginFrame();
            FrameTime time = clock.Tick();

            input.Begin();

            SDL_Event e;

            while (SDL_PollEvent(&e))
            {
                input.ProcessEvent(e);

                if (e.type == (uint)SDL_EventType.SDL_EVENT_QUIT)
                {
                    running = false;
                }
            }

            if (input.Keyboard.IsDown(Keys.Escape))
            {
                running = false;
            }

            if (input.Keyboard.IsDown(Keys.E))
            {
                window.SetRelativeMouseMode(true);
            }

            if (input.Keyboard.IsDown(Keys.R))
            {
                window.SetRelativeMouseMode(false);
            }

            renderer.HandleDebugInput(input.Keyboard);
            
            long worldStarted = Stopwatch.GetTimestamp();
            
            bool worldUpdate = worldLoader.Tick();
            profiler.Streaming = worldLoader.GetStatistics();
            profiler.Timings.CompletionMilliseconds = worldLoader.CompletionMilliseconds;
            profiler.Timings.WorldMilliseconds = Stopwatch.GetElapsedTime(worldStarted).TotalMilliseconds;
            bool controllerUpdate = activeViewController.Update(input, time);
            
            worldLoader.Recenter(activeViewController.GetIndex());

            if (controllerUpdate)
            {
                var viewpoint = activeViewController.GetViewpoint();
                camera.SetViewpoint(viewpoint);
            }

            if (controllerUpdate || worldUpdate)
            {
                long updateStarted = Stopwatch.GetTimestamp();
                renderer.UpdateWorld(camera, volume);
                profiler.Timings.RendererMilliseconds += Stopwatch.GetElapsedTime(updateStarted).TotalMilliseconds;
            }
            
            long renderStarted = Stopwatch.GetTimestamp();
            renderer.UpdateUi(camera);
            renderer.Render(camera);
            
            profiler.Timings.RendererMilliseconds += Stopwatch.GetElapsedTime(renderStarted).TotalMilliseconds;

            profiler.EndWork();
            long waitStarted = Stopwatch.GetTimestamp();
            frameLimiter?.Wait();
            
            profiler.Timings.LimiterMilliseconds = Stopwatch.GetElapsedTime(waitStarted).TotalMilliseconds;
            
            profiler.EndFrame(readGpuResources);
            benchmark.Record(profiler.LatestFrame);
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        worldLoader.Dispose();
        device.WaitIdle();

        renderer.Dispose();
        assetServer.Dispose();

        device.Dispose();
        window.Dispose();
        sdlRuntime.Dispose();
        fontSystem.Dispose();
        profiler.Dispose();

        disposed = true;
    }
}

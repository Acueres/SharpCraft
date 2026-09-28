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
using SharpCraft.World.Chunks;
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
    private readonly FrameLimiter frameLimiter;

    private readonly WorldLoader worldLoader;
    private readonly ChunkVolume volume;

    public App()
    {
        sdlRuntime = new SdlRuntime();
        window = new Window("SharpCraft", (int)DefaultWidth, (int)DefaultHeight);
        device = new GpuDevice("vulkan", window, debugInfo: true);
        fontSystem = new FontSystem();

        assetServer = new AssetServer(device);
        var blockRegistry = new BlockRegistry(assetServer);
        
        input = new InputHandler();

        var initialViewpoint = Viewpoint.LookAt(
            position: new Vector3(0f, 40f, 4f),
            target: Vector3.Zero,
            up: MathUtilities.Vector3Up
        );

        //activeViewController = new ObserverViewController(initialViewpoint);
        activeViewController = new OrbitViewController(
            target: Vector3.Zero,
            initialViewpoint: initialViewpoint,
            minimumDistance: 2f,
            maximumDistance: 500f
        );
        activeViewController.SetIndex(Chunk.WorldToChunkCoords(initialViewpoint.Position));
        
        camera = new Camera(initialViewpoint, DefaultWidth, DefaultHeight);

        frameLimiter = new FrameLimiter(60);

        volume = new ChunkVolume(8);
        var chunkGenerator = new ChunkGenerator(blockRegistry);
        var chunkMesher = new ChunkMesher(blockRegistry);
        worldLoader = new WorldLoader(volume, chunkGenerator, chunkMesher);
        worldLoader.BulkGenerate(Vector3.Zero);
        
        renderer = new Renderer(DefaultWidth, DefaultHeight, window, device, assetServer, chunkMesher, profiler);
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

            if (input.Keyboard.WasPressed(Keys.F3))
            {
                renderer.ToggleDebugOverlay();
            }
            
            long worldStarted = Stopwatch.GetTimestamp();
            Vec3<int> currentControllerIndex = Chunk.WorldToChunkCoords(activeViewController.GetPosition());

            if (activeViewController.GetIndex() != currentControllerIndex)
            {
                worldLoader.Recenter(activeViewController.GetPosition());
                activeViewController.SetIndex(currentControllerIndex);
            }
            
            bool worldUpdate = worldLoader.Tick();
            profiler.Streaming = worldLoader.GetStatistics();
            profiler.Timings.WorldMilliseconds = Stopwatch.GetElapsedTime(worldStarted).TotalMilliseconds;
            bool controllerUpdate = activeViewController.Update(input, time);

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
            renderer.UpdateUi();
            renderer.Render(camera);
            profiler.Timings.RendererMilliseconds += Stopwatch.GetElapsedTime(renderStarted).TotalMilliseconds;

            profiler.EndWork();
            long waitStarted = Stopwatch.GetTimestamp();
            frameLimiter.Wait();
            profiler.Timings.LimiterMilliseconds = Stopwatch.GetElapsedTime(waitStarted).TotalMilliseconds;
            profiler.EndFrame();
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        device.WaitIdle();

        renderer.Dispose();
        assetServer.Dispose();

        device.Dispose();
        window.Dispose();
        sdlRuntime.Dispose();
        fontSystem.Dispose();

        disposed = true;
    }
}

using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Input;
using SharpCraft.Platform;
using SharpCraft.Rendering;
using SharpCraft.Rendering.Text;
using SharpCraft.Time;
using SharpCraft.World.Blocks;
using SharpCraft.SharpMath;
using SharpCraft.World.Generation;
using SharpCraft.World.Meshing;
using SharpCraft.Rendering.View;

using SDL;
using System.Numerics;

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
    private IViewController activeViewController;
    
    private readonly FrameClock clock = new();
    private readonly FrameLimiter frameLimiter;

    public App()
    {
        sdlRuntime = new SdlRuntime();
        window = new Window("SharpCraft", (int)DefaultWidth, (int)DefaultHeight);
        device = new GpuDevice("vulkan", window, debugInfo: true);
        fontSystem = new FontSystem();

        assetServer = new AssetServer(device);
        var blockRegistry = new BlockRegistry(assetServer);
        
        var origin = Vec3<int>.Zero;
        
        var chunkGenerator = new ChunkGenerator(blockRegistry);
        var originChunk = chunkGenerator.GenerateChunk(origin);
        originChunk.XPos = chunkGenerator.GenerateChunk(new Vec3<int>(1, 0, 0));
        originChunk.XNeg = chunkGenerator.GenerateChunk(new Vec3<int>(-1, 0, 0));
        originChunk.ZPos = chunkGenerator.GenerateChunk(new Vec3<int>(0, 1, 0));
        originChunk.ZNeg = chunkGenerator.GenerateChunk(new Vec3<int>(0, -1, 0));
        originChunk.YPos = chunkGenerator.GenerateChunk(new Vec3<int>(0, 0, 1));
        originChunk.YNeg = chunkGenerator.GenerateChunk(new Vec3<int>(0, 0, -1));
        
        var chunkMesher = new ChunkMesher(blockRegistry);
        chunkMesher.Build(originChunk);
        
        renderer = new Renderer(DefaultWidth, DefaultHeight, window, device, assetServer, chunkMesher);
        input = new InputHandler();

        var initialViewpoint = Viewpoint.LookAt(
            position: new Vector3(0f, 40f, 4f),
            target: Vector3.Zero,
            up: MathUtilities.Vector3Up
        );
        
        activeViewController = new ObserverViewController(initialViewpoint);
        camera = new Camera(initialViewpoint, DefaultWidth, DefaultHeight);

        frameLimiter = new FrameLimiter(60);
        
        renderer.LoadGpuResources();
    }

    public void Run()
    {
        bool running = true;
        
        while (running)
        {
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

            if (activeViewController.Update(input, time))
            {
                var viewpoint = activeViewController.GetViewpoint();
                camera.SetViewpoint(viewpoint);
            }

            renderer.Update(time, camera);
            renderer.Render(time, camera);

            frameLimiter.Wait();
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

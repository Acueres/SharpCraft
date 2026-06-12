using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Input;
using SharpCraft.Platform;
using SharpCraft.Rendering;
using SharpCraft.Rendering.Text;
using SharpCraft.Time;
using SharpCraft.World.Blocks;

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

        renderer = new Renderer(DefaultWidth, DefaultHeight, window, device, assetServer);
        input = new InputHandler();
        camera = new Camera(new Vector3(0f, 2f, 4f), Vector3.Zero, DefaultWidth, DefaultHeight);

        frameLimiter = new FrameLimiter(60);
    }

    public void Run()
    {
        renderer.LoadGpuResources();

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

            camera.Update(input, time);
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

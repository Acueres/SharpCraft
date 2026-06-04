using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Input;
using SharpCraft.Platform;
using SharpCraft.Rendering;
using SharpCraft.Time;

using SDL;
using System.Numerics;
using SharpCraft.Rendering.Text;
using static SDL.SDL3;

namespace SharpCraft;

internal unsafe class App : IDisposable
{
    private const uint width = 1280;
    private const uint height = 720;

    private readonly AssetServer assetServer;

    private readonly Renderer renderer;
    private readonly SdlRuntime sdlRuntime;
    private readonly Window window;
    private readonly GpuDevice device;
    private readonly FontSystem fontSystem;

    private readonly InputHandler input;
    private readonly Camera camera;
    private readonly FrameClock clock = new();

    public App()
    {
        sdlRuntime = new SdlRuntime();
        window = new Window("SharpCraft", (int)width, (int)height);
        device = new GpuDevice("vulkan", window, debugInfo: true);
        fontSystem = new FontSystem();

        assetServer = new AssetServer(device);

        renderer = new Renderer(width, height, window, device, assetServer);
        input = new InputHandler();
        camera = new Camera(new Vector3(0f, 2f, 4f), Vector3.Zero, width, height);
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
            renderer.Render(camera);

            SDL_Delay(1);
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

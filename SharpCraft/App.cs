using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Input;
using SharpCraft.Platform;
using SharpCraft.Rendering;
using SharpCraft.Time;

using SDL;
using System.Numerics;

using static SDL.SDL3;

namespace SharpCraft;

internal unsafe class App : IDisposable
{
    private const uint width = 1280;
    private const uint height = 720;

    private readonly AssetServer assetServer;

    private readonly GraphicsDevice graphics;
    private readonly SdlRuntime sdlRuntime;
    private readonly Window window;
    private readonly GpuDevice device;

    private readonly InputHandler input;
    private readonly Camera camera;
    private readonly FrameClock clock = new();

    public App()
    {
        sdlRuntime = new SdlRuntime();
        window = new Window("SharpCraft", (int)width, (int)height);
        device = new GpuDevice("vulkan", window, debugInfo: true);

        assetServer = new AssetServer(device);
        assetServer.Load();

        graphics = new GraphicsDevice(width, height, window, device, assetServer);
        input = new InputHandler();
        camera = new Camera(new Vector3(0f, 0f, 4f), Vector3.Zero, width, height);
    }

    public void Run()
    {
        graphics.UploadMesh();

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
                    continue;
                }
            }

            if (input.Keyboard.IsDown(Keys.Escape))
            {
                running = false;
            }

            if (input.Keyboard.IsDown(Keys.E))
            {
                graphics.Window.SetRelativeMouseMode(true);
            }

            if (input.Keyboard.IsDown(Keys.R))
            {
                graphics.Window.SetRelativeMouseMode(false);
            }

            camera.Update(input, time);

            if (graphics.TryBeginFrame(out var frame))
            {
                camera.SetViewport(frame.Width, frame.Height);
                graphics.Draw(frame, camera);
            }

            SDL_Delay(1);
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        device.WaitIdle();

        graphics.Dispose();
        assetServer.Dispose();

        device.Dispose();
        window.Dispose();
        sdlRuntime.Dispose();

        disposed = true;
    }
}

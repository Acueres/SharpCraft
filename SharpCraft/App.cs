using System.Numerics;

using SDL;

using SharpCraft.Graphics;
using SharpCraft.Input;
using SharpCraft.Rendering;
using static SDL.SDL3;

namespace SharpCraft;

internal unsafe class App : IDisposable
{
    private const uint width = 1280;
    private const uint height = 720;

    private readonly GraphicsDevice graphics;
    private readonly InputHandler input;
    private readonly Camera camera;

    public App()
    {
        graphics = new GraphicsDevice(width, height, "SharpCraft");
        input = new InputHandler();
        camera = new Camera(new Vector3(0f, 0f, 4f), Vector3.Zero, width, height);
    }

    public void Run()
    {
        graphics.UploadMesh();

        bool running = true;

        while (running)
        {
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

            camera.Update(input);

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

        graphics.Dispose();
        disposed = true;
    }
}

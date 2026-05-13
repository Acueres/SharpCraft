using SDL;

using SharpCraft.Graphics;
using SharpCraft.Input;
using static SDL.SDL3;

namespace SharpCraft;

internal unsafe class App : IDisposable
{
    private const uint width = 1280;
    private const uint height = 720;

    private readonly GraphicsDevice graphics;
    private readonly InputHandler input;

    public App()
    {
        graphics = new GraphicsDevice(width, height, "SharpCraft");
        input = new InputHandler();
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

            graphics.Draw();
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

using SDL;

using static SDL.SDL3;

namespace SharpCraft;

internal unsafe class App : IDisposable
{
    private const uint width = 1280;
    private const uint height = 720;

    private readonly GraphicsDevice graphics;

    public App()
    {
        graphics = new GraphicsDevice(width, height, "SharpCraft");
    }

    public void Run()
    {
        graphics.UploadMesh();

        bool running = true;

        while (running)
        {
            SDL_Event e;

            while (SDL_PollEvent(&e))
            {
                if (e.type == (uint)SDL_EventType.SDL_EVENT_QUIT)
                    running = false;

                if (e.type == (uint)SDL_EventType.SDL_EVENT_KEY_DOWN &&
                    e.key.key == SDL_Keycode.SDLK_ESCAPE)
                {
                    running = false;
                }
            }

            graphics.Draw();
            SDL_Delay(1);
        }
    }

    public void Dispose()
    {
        graphics.Dispose();
    }
}

using SDL;

namespace SharpCraft.Input;

internal class InputHandler
{
    public Keyboard Keyboard => keyboard;

    private readonly Keyboard keyboard = new();

    public void Begin()
    {
        keyboard.Begin();
    }

    public void ProcessEvent(in SDL_Event e)
    {
        switch ((SDL_EventType)e.type)
        {
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
                {
                    Keys key = KeyMapper.ToKey((int)e.key.scancode);
                    keyboard.OnKeyDown(key, e.key.repeat);
                    break;
                }

            case SDL_EventType.SDL_EVENT_KEY_UP:
                {
                    Keys key = KeyMapper.ToKey((int)e.key.scancode);
                    keyboard.OnKeyUp(key);
                    break;
                }
        }
    }
}

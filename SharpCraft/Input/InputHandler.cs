using SDL;

namespace SharpCraft.Input;

internal class InputHandler
{
    public Keyboard Keyboard => keyboard;
    public Mouse Mouse => mouse;

    private readonly Keyboard keyboard = new();
    private readonly Mouse mouse = new();

    public void Begin()
    {
        keyboard.Begin();
        mouse.Begin();
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
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                {
                    MouseButton button = MouseButtonMapper.ToButton((int)e.button.Button);
                    mouse.OnButtonDown(button);
                    break;
                }
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                {
                    MouseButton button = MouseButtonMapper.ToButton((int)e.button.Button);
                    mouse.OnButtonUp(button);
                    break;
                }
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                {
                    float x = e.motion.x;
                    float y = e.motion.y;
                    float deltaX = e.motion.xrel;
                    float deltaY = e.motion.yrel;
                    mouse.OnMotion(x, y, deltaX, deltaY);
                    break;
                }
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                {
                    float scrollX = e.wheel.x;
                    float scrollY = e.wheel.y;

                    if (e.wheel.direction == SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED)
                    {
                        scrollX *= -1;
                        scrollY *= -1;
                    }

                    mouse.OnWheel(scrollX, scrollY);
                    break;
                }
        }
    }
}

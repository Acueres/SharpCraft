namespace SharpCraft.Input;

internal class Keyboard
{
    private readonly HashSet<Keys> down = [];
    private readonly HashSet<Keys> pressed = [];
    private readonly HashSet<Keys> released = [];

    internal void Begin()
    {
        pressed.Clear();
        released.Clear();
    }

    internal void OnKeyDown(Keys key, bool isRepeat)
    {
        if (key == Keys.None)
            return;

        if (!isRepeat && down.Add(key))
        {
            pressed.Add(key);
        }
    }

    internal void OnKeyUp(Keys key)
    {
        if (key == Keys.None)
            return;

        if (down.Remove(key))
        {
            released.Add(key);
        }
    }

    public bool IsDown(Keys key) => down.Contains(key);

    public bool WasPressed(Keys key) => pressed.Contains(key);

    public bool WasReleased(Keys key) => released.Contains(key);
}

namespace SharpCraft.Input;

internal class Mouse
{
    public float X { get; private set; }
    public float Y { get; private set; }

    public float DeltaX { get; private set; }
    public float DeltaY { get; private set; }

    public float ScrollX { get; private set; }
    public float ScrollY { get; private set; }

    private readonly HashSet<MouseButton> down = [];
    private readonly HashSet<MouseButton> pressed = [];
    private readonly HashSet<MouseButton> released = [];

    public bool IsDown(MouseButton button) => down.Contains(button);
    public bool WasPressed(MouseButton button) => pressed.Contains(button);
    public bool WasReleased(MouseButton button) => released.Contains(button);

    internal void Begin()
    {
        DeltaX = 0;
        DeltaY = 0;

        ScrollX = 0;
        ScrollY = 0;

        pressed.Clear();
        released.Clear();
    }

    internal void OnMotion(float x, float y, float deltaX, float deltaY)
    {
        X = x;
        Y = y;

        DeltaX += deltaX;
        DeltaY += deltaY;
    }

    internal void OnButtonDown(MouseButton button)
    {
        if (button == MouseButton.None)
            return;

        if (down.Add(button))
        {
            pressed.Add(button);
        }
    }

    internal void OnButtonUp(MouseButton button)
    {
        if (button == MouseButton.None)
            return;

        if (down.Remove(button))
        {
            released.Add(button);
        }
    }

    internal void OnWheel(float scrollX, float scrollY)
    {
        ScrollX += scrollX;
        ScrollY += scrollY;
    }
}

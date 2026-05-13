namespace SharpCraft.Input;

internal static class MouseButtonMapper
{
    private static readonly MouseButton[] map =
    [
        MouseButton.None,    // 0 — SDL uses 1-based indices
        MouseButton.Left,    // 1 — SDL_BUTTON_LEFT
        MouseButton.Middle,  // 2 — SDL_BUTTON_MIDDLE
        MouseButton.Right,   // 3 — SDL_BUTTON_RIGHT
        MouseButton.X1,      // 4 — SDL_BUTTON_X1
        MouseButton.X2,      // 5 — SDL_BUTTON_X2
    ];

    /// <summary>
    /// Converts an SDL3 mouse button index to a <see cref="MouseButton"/> value.
    /// Returns <see cref="MouseButton.None"/> for unknown or out-of-range indices.
    /// </summary>
    public static MouseButton ToButton(int sdlButton)
    {
        if ((uint)sdlButton >= (uint)map.Length)
            return MouseButton.None;

        return map[sdlButton];
    }

    /// <summary>
    /// Converts a <see cref="MouseButton"/> value back to its SDL3 button index.
    /// Returns 0 for <see cref="MouseButton.None"/>.
    /// </summary>
    public static int ToSdlButton(MouseButton button) => button switch
    {
        MouseButton.Left => 1,
        MouseButton.Middle => 2,
        MouseButton.Right => 3,
        MouseButton.X1 => 4,
        MouseButton.X2 => 5,
        _ => 0,
    };

}

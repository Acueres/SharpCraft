using SharpCraft.Input;

namespace SharpCraft.Diagnostics;

internal sealed class DebugPanelState
{
    public bool Detailed { get; private set; }
    public int Page { get; private set; }
    public int Scroll { get; private set; }
    public bool ToggleCompact { get; private set; }
    public int Revision { get; private set; }

    public void Handle(Keyboard keyboard)
    {
        ToggleCompact = false;
        if (keyboard.WasPressed(Keys.F3))
        {
            if (keyboard.IsDown(Keys.LeftShift) || keyboard.IsDown(Keys.RightShift))
            {
                Detailed = !Detailed;
                Revision++;
            }
            else ToggleCompact = true;
        }
        if (!Detailed) return;
        int page = Page;
        for (int i = 0; i < 4; i++)
            if (keyboard.WasPressed((Keys)((int)Keys.D1 + i))) page = i;
        if (keyboard.WasPressed(Keys.Tab) || keyboard.WasPressed(Keys.Right)) page = (Page + 1) % 4;
        if (keyboard.WasPressed(Keys.Left)) page = (Page + 3) % 4;
        if (page != Page) { Page = page; Scroll = 0; Revision++; }
        int scroll = Scroll;
        if (keyboard.WasPressed(Keys.Down)) scroll++;
        if (keyboard.WasPressed(Keys.Up)) scroll--;
        if (keyboard.WasPressed(Keys.PageDown)) scroll += 8;
        if (keyboard.WasPressed(Keys.PageUp)) scroll -= 8;
        if (keyboard.WasPressed(Keys.Home)) scroll = 0;
        scroll = Math.Max(0, scroll);
        if (scroll != Scroll) { Scroll = scroll; Revision++; }
    }

    public void OpenBenchmarks() { Detailed = true; Page = 3; Revision++; }
    public void ClampScroll(int maximum) => Scroll = Math.Clamp(Scroll, 0, Math.Max(0, maximum));
}

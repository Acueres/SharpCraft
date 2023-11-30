using System;

using Microsoft.Xna.Framework;


namespace SharpCraft.GUI
{
    abstract class GUIElement
    {
        public bool Inactive = false;

        public virtual void Draw(string data) { }
        public virtual void Draw() { }
        public virtual void Update(Point point, bool click) { }
        public virtual bool Clicked(Point point, bool click) { return false; }
    }
}

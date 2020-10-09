using Microsoft.Xna.Framework;


namespace SharpCraft
{
    public class Button
    {
        public string Texture { get; set; }
        public string Selector { get; set; }

        public Rectangle Rect { get; set; }

        public string Text { get; set; }

        public Vector2 TextPosition { get; set; }

        public bool IsSelected { get; set; }


        public Button(int x, int y, int width, int height, string texture, string selector, string text)
        {
            Texture = texture;
            Selector = selector;

            Rect = new Rectangle(x, y, width, height);

            Text = text;

            TextPosition = new Vector2(1.4f * x, y + 0.25f * height);

            IsSelected = false;
        }
    }
}

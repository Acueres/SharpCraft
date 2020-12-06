using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft.GUI
{
    class Label : GUIElement
    {
        SpriteBatch spriteBatch;

        string text;

        SpriteFont font;

        Vector2 position;

        Color color;


        public Label(SpriteBatch _spriteBatch, string _text, SpriteFont _font,
                     Vector2 _position, Color _color)
        {
            spriteBatch = _spriteBatch;
            text = _text;
            font = _font;
            position = _position;
            color = _color;
        }

        public override void Draw(string append)
        {
            spriteBatch.DrawString(font, text + append, position, color);
        }

        public override void Draw()
        {
            Draw(null);
        }
    }
}

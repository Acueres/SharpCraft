using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft.GUI
{
    class Label : GUIElement
    {
        SpriteBatch spriteBatch;

        string text;

        SpriteFont font;

        Texture2D texture;
        Rectangle rect;

        Vector2 textPosition;

        Color color;


        public Label(SpriteBatch _spriteBatch, Texture2D _texture, Rectangle _rect,
                     string _text, SpriteFont _font, Vector2 _textPosition, Color _color)
        {
            spriteBatch = _spriteBatch;
            texture = _texture;
            rect = _rect;
            text = _text;
            font = _font;
            textPosition = _textPosition;
            color = _color;
        }

        public Label(SpriteBatch _spriteBatch, Texture2D _texture, Rectangle _rect, Color _color)
        {
            spriteBatch = _spriteBatch;
            texture = _texture;
            rect = _rect;
            color = _color;
        }

        public Label(SpriteBatch _spriteBatch, string _text, SpriteFont _font, Vector2 _textPosition, Color _color)
        {
            spriteBatch = _spriteBatch;
            text = _text;
            font = _font;
            textPosition = _textPosition;
            color = _color;
        }

        public override void Draw(string append)
        {
            if (texture != null)
            {
                spriteBatch.Draw(texture, rect, Color.White);
            }

            if (text != null)
            {
                spriteBatch.DrawString(font, text + append, textPosition, color);
            }
        }

        public override void Draw()
        {
            if (texture != null)
            {
                spriteBatch.Draw(texture, rect, Color.White);
            }

            if (text != null)
            {
                Draw(null);
            }
        }
    }
}

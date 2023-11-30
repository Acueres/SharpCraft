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


        public Label(SpriteBatch spriteBatch, Texture2D texture, Rectangle rect,
                     string text, SpriteFont font, Vector2 textPosition, Color color)
        {
            this.spriteBatch = spriteBatch;
            this.texture = texture;
            this.rect = rect;
            this.text = text;
            this.font = font;
            this.textPosition = textPosition;
            this.color = color;
        }

        public Label(SpriteBatch spriteBatch, Texture2D texture, Rectangle rect, Color color)
        {
            this.spriteBatch = spriteBatch;
            this.texture = texture;
            this.rect = rect;
            this.color = color;
        }

        public Label(SpriteBatch spriteBatch, string text, SpriteFont font, Vector2 textPosition, Color color)
        {
            this.spriteBatch = spriteBatch;
            this.text = text;
            this.font = font;
            this.textPosition = textPosition;
            this.color = color;
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

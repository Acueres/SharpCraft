using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft
{
    class Button
    {
        public bool Selected;
        public bool Inactive;

        SpriteBatch spriteBatch;

        Texture2D texture;
        Texture2D selector;
        Texture2D shading;

        SpriteFont font;

        Rectangle rect;

        string text;

        Vector2 textPosition;


        public Button(SpriteBatch _spriteBatch, int x, int y, int width, int height,
                      Texture2D _texture, Texture2D _selector, SpriteFont _font, string _text)
        {
            spriteBatch = _spriteBatch;

            texture = _texture;
            selector = _selector;
            font = _font;

            rect = new Rectangle(x, y, width, height);

            text = _text;

            textPosition = new Vector2(x + width / 2, y + height / 4);

            Selected = false;
            Inactive = false;
        }

        public void SetShading(GraphicsDeviceManager graphics)
        {
            shading = new Texture2D(graphics.GraphicsDevice, rect.Width, rect.Height);

            Color[] shadingColor = new Color[rect.Width * rect.Height];
            for (int i = 0; i < shadingColor.Length; i++)
                shadingColor[i] = new Color(Color.Black, 0.5f);

            shading.SetData(shadingColor);
        }

        public void Draw()
        {
            Vector2 textSize = font.MeasureString(text) / 2;
            textSize.Y = 0;

            spriteBatch.Draw(texture, rect, Color.White);
            spriteBatch.DrawString(font, text, textPosition - textSize, Color.White);

            if (Inactive)
            {
                spriteBatch.Draw(shading, rect, Color.White);
            }

            if (Selected)
            {
                spriteBatch.Draw(selector, rect, Color.White);
            }

            Selected = false;
        }

        public void Draw(object data)
        {
            string newText = text + $": {data}";
            Vector2 textSize = font.MeasureString(newText) / 2;
            textSize.Y = 0;

            spriteBatch.Draw(texture, rect, Color.White);
            spriteBatch.DrawString(font, newText, textPosition - textSize, Color.White);

            if (Inactive)
            {
                spriteBatch.Draw(shading, rect, Color.White);
            }

            if (Selected)
            {
                spriteBatch.Draw(selector, rect, Color.White);
            }

            Selected = false;
        }

        public bool Contains(Point point)
        {
            return !Inactive && rect.Contains(point);
        }
    }
}

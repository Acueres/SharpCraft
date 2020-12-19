using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft.GUI
{
    class SaveSlot
    {
        SpriteBatch spriteBatch;

        Texture2D selector;

        SpriteFont font;

        Rectangle rect;


        public SaveSlot(SpriteBatch spriteBatch, int x, int width, int height,
                        Texture2D selector, SpriteFont font)
        {
            this.spriteBatch = spriteBatch;
            this.selector = selector;
            this.font = font;

            rect = new Rectangle(x, 0, width, height);
        }

        public void DrawAt(int y, Save save, bool selected)
        {
            Vector2 namePosition = new Vector2(rect.X + 74, y + 10);
            Vector2 datePosition = new Vector2(rect.X + 74, y + 30);

            rect.Y = y;

            spriteBatch.Draw(save.Icon, new Rectangle(rect.X + 10, y + 10, 64, 64), Color.White);
            spriteBatch.DrawString(font, save.Name, namePosition, Color.White);
            spriteBatch.DrawString(font, "Last modified on: " + save.Parameters.Date, datePosition, Color.DarkGray);

            if (selected)
            {
                spriteBatch.Draw(selector, rect, Color.White);
            }
            else
            {
                spriteBatch.Draw(selector, rect, Color.DarkGray);
            }
        }

        public bool ContainsAt(int y, Point point)
        {
            rect.Y = y;

            return rect.Contains(point);
        }
    }
}

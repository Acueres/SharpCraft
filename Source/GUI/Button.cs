using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft.GUI
{
    class Button : GUIElement
    {
        SpriteBatch spriteBatch;

        Action action;

        Texture2D texture;
        Texture2D selector;
        Texture2D shading;

        SpriteFont font;

        Rectangle rect;

        string baseText;

        Vector2 textPosition;


        public Button(GraphicsDevice graphics, SpriteBatch spriteBatch, string text,
                      SpriteFont font, int x, int y, int width, int height,
                      Texture2D texture, Texture2D selector, Action a = null)
        {
            this.spriteBatch = spriteBatch;
            this.texture = texture;
            this.selector = selector;
            this.font = font;
            action = a;

            rect = new Rectangle(x, y, width, height);

            baseText = text;

            textPosition = new Vector2(x + width / 2, y + height / 4);

            shading = new Texture2D(graphics, rect.Width, rect.Height);
            Color[] shadingColor = new Color[rect.Width * rect.Height];
            for (int i = 0; i < shadingColor.Length; i++)
            {
                shadingColor[i] = new Color(Color.Black, 0.5f);
            }
            shading.SetData(shadingColor);
        }

        public void SetAction(Action a)
        {
            action = a;
        }

        public override void Draw(string data)
        {
            MouseState currentMouseState = Mouse.GetState();
            Point mouseLoc = new Point(currentMouseState.X, currentMouseState.Y);

            string text = baseText + $"{data}";
            Vector2 textSize = font.MeasureString(text) / 2;
            textSize.Y = 0;

            spriteBatch.Draw(texture, rect, Color.White);
            spriteBatch.DrawString(font, text, textPosition - textSize, Color.White);

            if (Inactive)
            {
                spriteBatch.Draw(shading, rect, Color.White);
            }
            else if (rect.Contains(mouseLoc))
            {
                spriteBatch.Draw(selector, rect, Color.White);
            }
        }

        public override void Draw()
        {
            Draw(null);
        }

        public override void Update(Point mouseLoc, bool click)
        {
            if (action is null)
            {
                return;
            }

            if (!Inactive && rect.Contains(mouseLoc) && click)
            {
                action();
            }
        }

        public override bool Clicked(Point mouseLoc, bool click)
        {
            return !Inactive && rect.Contains(mouseLoc) && click;
        }
    }
}
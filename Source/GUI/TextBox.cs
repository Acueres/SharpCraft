using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft.GUI
{
    class TextBox : GUIElement
    {
        public bool Selected;

        StringBuilder stringBuilder;
        char inputChar;

        GameWindow window;
        SpriteBatch spriteBatch;

        Texture2D texture;
        Texture2D selector;

        SpriteFont font;

        Rectangle rect;
        Vector2 textPosition;


        public TextBox(GameWindow _window, GraphicsDeviceManager graphics, SpriteBatch _spriteBatch,
                      int x, int y, int width, int height, Texture2D _selector, SpriteFont _font)
        {
            window = _window;
            spriteBatch = _spriteBatch;
            selector = _selector;
            font = _font;

            rect = new Rectangle(x, y, width, height);

            texture = new Texture2D(graphics.GraphicsDevice, rect.Width, rect.Height);
            Color[] textureColor = new Color[rect.Width * rect.Height];
            for (int i = 0; i < textureColor.Length; i++)
            {
                textureColor[i] = Color.Black;
            }
            texture.SetData(textureColor);

            stringBuilder = new StringBuilder(30);

            textPosition = new Vector2(x + 10, y + height / 4);
        }

        public override void Draw()
        {
            if (Inactive)
            {
                return;
            }

            spriteBatch.Draw(texture, rect, Color.White);
            spriteBatch.DrawString(font, stringBuilder, textPosition, Color.White);

            if (Selected)
            {
                spriteBatch.Draw(selector, rect, Color.White);
            }
            else
            {
                spriteBatch.Draw(selector, rect, Color.DarkGray);
            }
        }

        public void Update(Point mouseLoc, KeyboardState currentKeyboardState, 
            KeyboardState previousKeyboardState, bool leftClick, bool rightClick)
        {
            if (Inactive)
            {
                return;
            }

            if (rect.Contains(mouseLoc))
            {
                if (leftClick)
                {
                    Selected = true;
                }

                if (Selected)
                {
                    window.TextInput += TextInput;
                }

                if (Util.KeyPressed(Keys.Back, currentKeyboardState, previousKeyboardState))
                {
                    RemoveChar();
                }
                else if (font.Characters.Contains(inputChar) && currentKeyboardState.GetPressedKeyCount() > 0 &&
                         Util.KeyPressed(currentKeyboardState.GetPressedKeys()[0], currentKeyboardState, previousKeyboardState))
                {
                    AddChar(inputChar);
                }
            }

            else if (leftClick || rightClick)
            {
                Selected = false;
            }
        }

        public override string ToString()
        {
            return stringBuilder.ToString();
        }

        void AddChar(char inputChar)
        {
            if (font.MeasureString(stringBuilder).X + 30 < rect.Width)
            {
                stringBuilder.Append(inputChar);
            }
        }

        void RemoveChar()
        {
            if (stringBuilder.Length > 0)
            {
                stringBuilder.Length--;
            }
        }

        void TextInput(object sender, TextInputEventArgs args)
        {
            inputChar = args.Character;
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft.GUI.Components
{
    class Scroller
    {
        public int Start { get; private set; }
        public int End { get; private set; }

        SpriteBatch spriteBatch;

        Rectangle rect;
        Texture2D texture;

        int step;
        int maxIndex;


        public Scroller(SpriteBatch spriteBatch, Texture2D texture, int maxIndex, int step, Rectangle rect)
        {
            this.spriteBatch = spriteBatch;
            this.texture = texture;
            this.rect = rect;

            this.maxIndex = maxIndex;
            this.step = step;

            Start = 0;
            End = maxIndex > 5 ? 5 : maxIndex;
        }

        public void Draw()
        {
            spriteBatch.Draw(texture, rect, Color.White);
        }

        public void Update(MouseState currentMouseState, MouseState previousMouseState, Point mouseLoc)
        {
            if (rect.Contains(mouseLoc) && currentMouseState.LeftButton == ButtonState.Pressed)
            {
                if (currentMouseState.Y - previousMouseState.Y > 2 &&
                    End < maxIndex)
                {
                    rect.Y += step;
                    Start++;
                    End++;
                }
                else if (currentMouseState.Y - previousMouseState.Y < -2 &&
                    Start > 0)
                {
                    rect.Y -= step;
                    Start--;
                    End--;
                }

                return;
            }

            if (currentMouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue < 0 &&
                End < maxIndex)
            {
                rect.Y += step;
                Start++;
                End++;
            }
            else if (currentMouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue > 0 &&
                Start > 0)
            {
                rect.Y -= step;
                Start--;
                End--;
            }
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpCraft.World;

namespace SharpCraft.Utility
{
    static class Util
    {
        public static string Title(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            return char.ToUpper(str[0]) + str.Substring(1).ToLower();
        }

        public static Faces MaxFace(IEnumerable<LightValue> faceValues)
        {
            Faces face = Faces.YPos;
            LightValue maxValue = LightValue.Null;
            int index = 0;

            foreach (LightValue value in faceValues)
            {
                if (value > maxValue)
                {
                    maxValue = value;
                    face = (Faces)index;
                }
                index++;
            }

            return face;
        }

        public static char MaxVectorComponent(Vector3 vector)
        {
            float max = Math.Abs(vector.X);
            char component = 'X';

            if (max < Math.Abs(vector.Y))
            {
                max = Math.Abs(vector.Y);
                component = 'Y';
            }

            if (max < Math.Abs(vector.Z))
            {
                component = 'Z';
            }

            return component;
        }

        public static Texture2D GetColoredTexture(GraphicsDevice graphics, int width, int height, Color color, float alpha = 1f)
        {
            var texture = new Texture2D(graphics, width, height);
            Color[] data = new Color[width * height];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new Color(color, alpha);
            }
            texture.SetData(data);

            return texture;
        }

        //Input utility functions
        public static bool KeyPressed(Keys key, KeyboardState currentKeyboardState, KeyboardState previousKeyboardState)
        {
            return currentKeyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);
        }

        public static bool KeyReleased(Keys key, KeyboardState currentKeyboardState, KeyboardState previousKeyboardState)
        {
            return !currentKeyboardState.IsKeyDown(key) && previousKeyboardState.IsKeyDown(key);
        }

        public static bool RightButtonClicked(MouseState currentMouseState, MouseState previousMouseState)
        {
            return currentMouseState.RightButton == ButtonState.Pressed &&
                   previousMouseState.RightButton == ButtonState.Released;
        }

        public static bool LeftButtonClicked(MouseState currentMouseState, MouseState previousMouseState)
        {
            return currentMouseState.LeftButton == ButtonState.Pressed &&
                   previousMouseState.LeftButton == ButtonState.Released;
        }
    }
}

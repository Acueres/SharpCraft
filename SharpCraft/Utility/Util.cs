using System;
using System.Linq;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;


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

        public static int ArgMax(byte[] arr)
        {
            int index = 0;
            int count = 0;
            byte max = arr.Max();

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == max)
                {
                    count++;
                    index = i;
                }
            }

            if (count > 1)
            {
                Random rnd = new();
                index = rnd.Next(0, 6);

                while (arr[index] != max)
                {
                    index = rnd.Next(0, 6);
                }

                return index;
            }

            return index;
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

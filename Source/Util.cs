﻿using System;
using System.Linq;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;


namespace SharpCraft
{
    static class Util
    {
        //Legacy rendering code
        public static void DrawCubeSide(VertexPositionTexture[] side, Texture2D texture,
            GraphicsDeviceManager graphics, BasicEffect effect, Matrix view, Matrix projection, Vector3 position)
        {
            effect.TextureEnabled = true;
            effect.Texture = texture;

            effect.World = Matrix.CreateTranslation(position);
            effect.View = view;
            effect.Projection = projection;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, side, 0, 2);
            }
        }

        public static Texture2D Screenshot(GraphicsDevice graphicsDevice, int screenWidth, int screenHeight, string path)
        {
            Color[] colorData = new Color[screenHeight * screenWidth];
            graphicsDevice.GetBackBufferData(colorData);
            Texture2D screenshot = new Texture2D(graphicsDevice, screenWidth, screenHeight);
            screenshot.SetData(colorData);

            Stream stream = File.Create(path);
            screenshot.SaveAsPng(stream, screenWidth, screenHeight);

            return screenshot;
        }

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
                Random rnd = new Random();
                index = rnd.Next(0, 6);

                while (arr[index] != max)
                {
                    index = rnd.Next(0, 6);
                }

                return index;
            }

            return index;
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

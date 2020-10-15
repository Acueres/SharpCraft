using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace SharpCraft
{
    public static class Util
    {
        //Drawing utilities
        public static void DrawCubeSide(VertexPositionTexture[] side, Texture2D texture, GraphicsDeviceManager graphics, BasicEffect effect, Matrix view, Matrix projection, Vector3 position)
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

        public static string Title(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            return char.ToUpper(str[0]) + str.Substring(1).ToLower();
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
            return currentMouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released;
        }

        public static bool LeftButtonClicked(MouseState currentMouseState, MouseState previousMouseState)
        {
            return currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
        }
    }

    static class Parameters
    {
        public static bool GameLoading = false;
        public static bool GameStarted = false;
        public static bool GamePaused = false;
        public static bool ExitedGameMenu = false;
        public static bool ExitedToMainMenu = false;
        public static bool IsFlying = false;

        public static int Seed = 0;
        public static int ChunkSize = 16;
        public static int RenderDistance = 8;

        public static string WorldType = "Default";

        public static Vector3 Position = Vector3.Zero;
        public static Vector3 Direction = new Vector3(0, -0.5f, -1f);

        public static ushort?[] Inventory = new ushort?[9];
    }

    class BlockData
    {
        public string type, front, back, top, bottom, right, left;
    }

    class BlockName
    {
        public string name, type;
    }

    class SaveParameters
    {
        public int seed;
        public bool isFlying;
        public float X, Y, Z;
        public float dirX, dirY, dirZ;
        public ushort?[] inventory;
        public string worldType;
    }

    class Settings
    {
        public int renderDistance;
        public string worldType;
    }
}

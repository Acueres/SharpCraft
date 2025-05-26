using System;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft.Persistence
{
    class ScreenshotTaker
    {
        public bool TakeScreenshot;

        GraphicsDevice graphics;
        int screenWidth, screenHeight;


        public ScreenshotTaker(GraphicsDevice graphics, int screenWidth, int screenHeight)
        {
            this.graphics = graphics;
            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;

            if (!Directory.Exists("Screenshots"))
            {
                Directory.CreateDirectory("Screenshots");
            }
        }

        public void Screenshot(string name)
        {
            Color[] colorData = new Color[screenHeight * screenWidth];
            graphics.GetBackBufferData(colorData);
            Texture2D screenshot = new(graphics, screenWidth, screenHeight);
            screenshot.SetData(colorData);

            name = name.Replace('.', '_');
            name = name.Replace(' ', '_');
            name = name.Replace(':', '.');

            Stream stream = File.Create($@"Screenshots/{name + ".png"}");
            screenshot.SaveAsPng(stream, screenWidth, screenHeight);

            TakeScreenshot = false;
        }

        public void SaveIcon(string saveName, out Texture2D icon)
        {
            Color[] colorData = new Color[screenHeight * screenWidth];
            graphics.GetBackBufferData(colorData);
            icon = new Texture2D(graphics, screenWidth, screenHeight);
            icon.SetData(colorData);

            Stream stream = File.Create($"Saves/{saveName}/save_icon.png");
            icon.SaveAsPng(stream, screenWidth, screenHeight);
        }
    }
}

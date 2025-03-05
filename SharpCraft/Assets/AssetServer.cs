using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using System.Collections.Generic;
using System.IO;

using SharpCraft.Utilities;

namespace SharpCraft.Assets
{
    public class AssetServer(ContentManager content, string rootDirectory)
    {
        readonly ContentManager content = content;
        readonly string rootDirectory = rootDirectory;

        readonly Dictionary<string, Texture2D> menuTextures = [];
        readonly List<Texture2D> blockTextures = [];
        readonly List<SpriteFont> fonts = [];

        Effect effect;

        public Effect GetEffect => effect.Clone();
        public Texture2D GetBlockTexture(ushort index) => blockTextures[index];
        public Texture2D GetMenuTexture(string name) => menuTextures[name];
        public SpriteFont GetFont(int index) => fonts[index];

        public void Load(GraphicsDevice graphics)
        {
            LoadBlocks(graphics);
            LoadMenu();
            LoadFonts();
            LoadEffects();
        }

        void LoadBlocks(GraphicsDevice graphics)
        {
            string[] blockTextureNames = Directory.GetFiles($"{rootDirectory}/Textures/Blocks", ".");

            blockTextures.Add(Util.GetColoredTexture(graphics, 64, 64, Color.Transparent, 1));
            for (int i = 0; i < blockTextureNames.Length; i++)
            {
                string textureName = blockTextureNames[i].Split('\\')[1].Split('.')[0];
                blockTextures.Add(content.Load<Texture2D>("Textures/Blocks/" + textureName));
            }
        }

        void LoadMenu()
        {
            string[] textureNames = [.. Directory.GetFiles($"{rootDirectory}/Textures/Menu", ".")];

            for (int i = 0; i < textureNames.Length; i++)
            {
                string textureName = textureNames[i].Split('\\')[1].Split('.')[0];
                menuTextures.Add(textureName, content.Load<Texture2D>("Textures/Menu/" + textureName));
            }
        }

        void LoadFonts()
        {
            fonts.Add(content.Load<SpriteFont>("Fonts/font14"));
            fonts.Add(content.Load<SpriteFont>("Fonts/font24"));
        }

        void LoadEffects()
        {
            effect = content.Load<Effect>("Effects/BlockEffect");
        }
    }
}

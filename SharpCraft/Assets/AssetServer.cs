using System.Collections.Generic;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Rendering;
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

        TextureAtlas atlas;

        const int textureSize = 64;

        public Effect Effect => effect.Clone();
        public Texture2D GetBlockTexture(ushort index) => blockTextures[index];
        public Texture2D GetMenuTexture(string name) => menuTextures[name];
        public SpriteFont GetFont(int index) => fonts[index];
        public TextureAtlas Atlas => atlas;

        public void Load(GraphicsDevice graphics)
        {
            LoadBlocks(graphics);
            LoadMenu();
            LoadFonts();
            LoadEffects();
            BuildAtlas(graphics);
        }

        void LoadBlocks(GraphicsDevice graphics)
        {
            string blocksPath = Path.Combine(rootDirectory, "Textures", "Blocks");
            string[] texturePaths = Directory.GetFiles(blocksPath, "*.xnb");

            blockTextures.Add(Util.GetColoredTexture(graphics, textureSize, textureSize, Color.Transparent, 1));
            foreach (string texturePath in texturePaths)
            {
                string textureName = Path.GetFileNameWithoutExtension(texturePath);
                string assetsTexturePath = Path.Combine("Textures", "Blocks", textureName);
                blockTextures.Add(content.Load<Texture2D>(assetsTexturePath));
            }
        }

        void LoadMenu()
        {
            string menuPath = Path.Combine(rootDirectory, "Textures", "Menu");
            string[] texturePaths = Directory.GetFiles(menuPath, "*.xnb");

            foreach (string texturePath in texturePaths)
            {
                string textureName = Path.GetFileNameWithoutExtension(texturePath);
                string assetsTexturePath = Path.Combine("Textures", "Menu", textureName);
                menuTextures.Add(textureName, content.Load<Texture2D>(assetsTexturePath));
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

        void BuildAtlas(GraphicsDevice graphics)
        {
            atlas = new TextureAtlas(textureSize, blockTextures.Count, graphics, GetBlockTexture);
        }
    }
}

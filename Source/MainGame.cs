using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;


namespace SharpCraft
{
    public class MainGame : Game
    {
        GraphicsDeviceManager graphics;

        int size;
        BasicEffect effect;

        Player player;
        World world;
        GameMenu gameMenu;
        Renderer renderer;
        SaveHandler saveHandler;

        Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        Dictionary<ushort, string> blockIndices = new Dictionary<ushort, string>();
        Dictionary<string, ushort> blockNames = new Dictionary<string, ushort>();
        Dictionary<ushort, ushort[]> multifaceBlocks = new Dictionary<ushort, ushort[]>();
        bool[] transparentBlocks;
        Texture2D[] blockTextures;
        SpriteFont[] fonts;


        public MainGame()
        {
            graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 854,
                PreferredBackBufferHeight = 480,
                IsFullScreen = false
            };

            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromMilliseconds(16);

            //graphics.GraphicsProfile = GraphicsProfile.Reach;

            Content.RootDirectory = "Assets";
        }

        protected override void Initialize()
        {
            LoadAssets();

            size = 16;
            int renderDistance = 8;
            effect = new BasicEffect(graphics.GraphicsDevice);

            saveHandler = new SaveHandler();
            gameMenu = new GameMenu(this, graphics, textures, blockTextures, blockIndices, fonts);
            world = new World(size, renderDistance, blockTextures.Length, gameMenu, saveHandler,
                              blockNames, multifaceBlocks, transparentBlocks);
            player = new Player(graphics, new Vector3(0f, 0f, 0f), new Vector3(0f, -0.5f, -1f));
            renderer = new Renderer(graphics, effect, size, world.Region, blockTextures);

            world.SetPlayer(player, blockNames);

            base.Initialize();
        }

        protected override void LoadContent()
        {

        }

        protected override void UnloadContent()
        {
            Content.Unload();
        }

        protected override void Update(GameTime gameTime)
        {
            if (IsActive)
            {
                if (!Parameters.GamePaused)
                {
                    player.Update(gameTime);
                    world.Update();
                }

                gameMenu.Update();

                base.Update(gameTime);
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            graphics.GraphicsDevice.Clear(Color.SkyBlue);

            renderer.Draw(world.ActiveChunks, player);
            gameMenu.Draw((int)Math.Round(1 / gameTime.ElapsedGameTime.TotalSeconds));

            base.Draw(gameTime);
        }


        //Utility methods
        void LoadAssets()
        {
            LoadBlocks();

            //Load menu textures
            var textureNames = Directory.GetFiles("Assets/Textures/Menu", ".").ToArray();

            for (int i = 0; i < textureNames.Length; i++)
            {
                var t = textureNames[i].Split('\\')[1].Split('.')[0];
                textures.Add(t, Content.Load<Texture2D>("Textures/Menu/" + t));
            }

            fonts = new SpriteFont[2];

            fonts[0] = Content.Load<SpriteFont>("Fonts/font14");
            fonts[1] = Content.Load<SpriteFont>("Fonts/font24");
        }

        void LoadBlocks()
        {
            List<BlockData> blockData;
            using (StreamReader r = new StreamReader("Assets/block_data.json"))
            {
                string json = r.ReadToEnd();
                blockData = JsonConvert.DeserializeObject<List<BlockData>>(json);
            }

            List<BlockName> blockNameData;
            using (StreamReader r = new StreamReader("Assets/block_names.json"))
            {
                string json = r.ReadToEnd();
                blockNameData = JsonConvert.DeserializeObject<List<BlockName>>(json);
            }

            var blockTextureNames = Directory.GetFiles("Assets/Textures/Blocks", ".").ToArray();
            blockTextures = new Texture2D[blockTextureNames.Length];

            for (ushort i = 0; i < blockTextureNames.Length; i++)
            {
                var t = blockTextureNames[i].Split('\\')[1].Split('.')[0];
                blockTextures[i] = Content.Load<Texture2D>("Textures/Blocks/" + t);
                blockNames.Add(t, i);
            }

            string[] sides = new string[] { "front", "back", "top", "bottom", "right", "left" };

            foreach (var entry in blockData)
            {
                if (entry.type is null)
                    continue;

                var sideData = entry.GetType().GetFields().
                    ToDictionary(prop => prop.Name, prop => prop.GetValue(entry));

                ushort[] arr = new ushort[6];

                for (int i = 0; i < sides.Length; i++)
                {
                    if (sideData[sides[i]] is null)
                    {
                        arr[i] = blockNames[sideData["type"].ToString()];
                    }
                    else
                    {
                        arr[i] = blockNames[sideData[sides[i]].ToString()];
                    }
                }

                multifaceBlocks.Add(blockNames[entry.type], arr);
            }

            foreach (var entry in blockNameData)
            {
                string name = entry.name is null ? Util.Title(entry.type) : entry.name;

                blockIndices.Add(blockNames[entry.type], name);
            }

            blockNames = blockIndices.ToDictionary(x => x.Value, x => x.Key);

            transparentBlocks = new bool[blockTextures.Length];

            for (int i = 0; i < transparentBlocks.Length; i++)
            {
                if (blockNames["Glass"] == i)
                    transparentBlocks[i] = true;
                else if (blockNames["Water"] == i)
                    transparentBlocks[i] = true;
            }
        }
    }

    static class Parameters
    {
        public static bool GamePaused = false;
        public static bool ExitedMenu = false;
    }

    class BlockData
    {
        public string type, front, back, top, bottom, right, left;
    }

    class BlockName
    {
        public string name, type;
    }
}

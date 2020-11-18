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
        Effect effect;

        Player player;
        World world;
        GameMenu gameMenu;
        MainMenu mainMenu;
        Renderer renderer;
        SaveHandler saveHandler;

        Dictionary<string, Texture2D> menuTextures = new Dictionary<string, Texture2D>();
        Dictionary<ushort, string> blockNames = new Dictionary<ushort, string>();
        Dictionary<string, ushort> blockIndices = new Dictionary<string, ushort>();
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

            Content.RootDirectory = "Assets";
        }

        protected override void Initialize()
        {
            LoadAssets();

            mainMenu = new MainMenu(this, graphics, menuTextures, fonts);

            base.Initialize();
        }

        void LoadAssets()
        {
            LoadBlocks();

            //Load menu textures
            var textureNames = Directory.GetFiles("Assets/Textures/Menu", ".").ToArray();

            for (int i = 0; i < textureNames.Length; i++)
            {
                var t = textureNames[i].Split('\\')[1].Split('.')[0];
                menuTextures.Add(t, Content.Load<Texture2D>("Textures/Menu/" + t));
            }

            fonts = new SpriteFont[2];

            fonts[0] = Content.Load<SpriteFont>("Fonts/font14");
            fonts[1] = Content.Load<SpriteFont>("Fonts/font24");

            effect = Content.Load<Effect>("Shaders/BlockEffect");
        }

        protected override void UnloadContent()
        {
            Content.Unload();

            SaveSettings();
        }

        protected override void Update(GameTime gameTime)
        {
            if (IsActive)
            {
                if (Parameters.GameStarted)
                {
                    if (!Parameters.GamePaused)
                    {
                        player.Update(gameTime);
                        world.Update();
                    }
                    
                    gameMenu.Update();
                }
                else if (Parameters.GameLoading)
                {
                    NewGame();
                }
                else if (Parameters.ExitedToMainMenu)
                {
                    SaveSettings();
                    player = null;

                    world = null;

                    saveHandler = null;

                    gameMenu = null;

                    renderer = null;

                    GC.Collect();

                    IsMouseVisible = true;
                    Parameters.ExitedToMainMenu = false;
                }
                else
                {
                    mainMenu.Update();
                }
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            if (Parameters.GameStarted)
            {
                graphics.GraphicsDevice.Clear(Color.SkyBlue);

                renderer.Draw(world.ActiveChunks, player);
                gameMenu.Draw((int)Math.Round(1 / gameTime.ElapsedGameTime.TotalSeconds));
            }
            else if (Parameters.GameLoading)
            {
                mainMenu.DrawLoadingScreen();
            }
            else
            {
                mainMenu.Draw();
            }

            base.Draw(gameTime);
        }

        void NewGame()
        {
            Parameters.GameLoading = false;
            Parameters.GameStarted = true;

            saveHandler = new SaveHandler();
            gameMenu = new GameMenu(this, graphics, menuTextures, blockTextures, blockNames, fonts);
            world = new World(blockTextures.Length, gameMenu, saveHandler,
                              blockIndices, multifaceBlocks, transparentBlocks);
            player = new Player(graphics, Parameters.Position, Parameters.Direction);
            renderer = new Renderer(graphics, effect, world.Region, blockTextures);

            world.SetPlayer(player);
        }

        void SaveSettings()
        {
            if (player != null)
            {
                List<SaveParameters> data = new List<SaveParameters>(1)
                {
                    new SaveParameters()
                    {
                        seed = Parameters.Seed,
                        isFlying = player.Flying,
                        X = player.Position.X,
                        Y = player.Position.Y,
                        Z = player.Position.Z,
                        dirX = player.Camera.Direction.X,
                        dirY = player.Camera.Direction.Y,
                        dirZ = player.Camera.Direction.Z,
                        inventory = Parameters.Inventory,
                        worldType = Parameters.WorldType
                    }
                };

                string json = JsonConvert.SerializeObject(data);
                string path = Directory.GetCurrentDirectory() + @"\Save\parameters.json";

                File.WriteAllText(path, json);
            }
        }

        void LoadBlocks()
        {
            List<MultifaceData> blockData;
            using (StreamReader r = new StreamReader("Assets/multiface_blocks.json"))
            {
                string json = r.ReadToEnd();
                blockData = JsonConvert.DeserializeObject<List<MultifaceData>>(json);
            }

            List<BlockData> blockNameData;
            using (StreamReader r = new StreamReader("Assets/blocks.json"))
            {
                string json = r.ReadToEnd();
                blockNameData = JsonConvert.DeserializeObject<List<BlockData>>(json);
            }

            var blockTextureNames = Directory.GetFiles("Assets/Textures/Blocks", ".").ToArray();
            blockTextures = new Texture2D[blockTextureNames.Length];

            for (ushort i = 0; i < blockTextureNames.Length; i++)
            {
                var t = blockTextureNames[i].Split('\\')[1].Split('.')[0];
                blockTextures[i] = Content.Load<Texture2D>("Textures/Blocks/" + t);
                blockIndices.Add(t, i);
            }

            string[] sides = new string[] { "front", "back", "top", "bottom", "right", "left" };

            foreach (var entry in blockData)
            {
                if (entry.type is null)
                    continue;

                var sideData = entry.GetType().GetFields().
                    ToDictionary(x => x.Name, x => x.GetValue(entry));

                ushort[] arr = new ushort[6];

                for (int i = 0; i < sides.Length; i++)
                {
                    if (sideData[sides[i]] is null)
                    {
                        arr[i] = blockIndices[sideData["type"].ToString()];
                    }
                    else
                    {
                        arr[i] = blockIndices[sideData[sides[i]].ToString()];
                    }
                }

                multifaceBlocks.Add(blockIndices[entry.type], arr);
            }

            foreach (var entry in blockNameData)
            {
                string name = entry.name is null ? Util.Title(entry.type) : entry.name;

                blockNames.Add(blockIndices[entry.type], name);
            }

            blockIndices = blockNames.ToDictionary(x => x.Value, x => x.Key);

            transparentBlocks = new bool[blockTextures.Length];

            for (int i = 0; i < transparentBlocks.Length; i++)
            {
                if (blockIndices["Glass"] == i)
                    transparentBlocks[i] = true;
                else if (blockIndices["Water"] == i)
                    transparentBlocks[i] = true;
            }
        }
    }
}

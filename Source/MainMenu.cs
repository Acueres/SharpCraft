using System;
using System.Collections.Generic;
using System.IO;
using System.Data.SQLite;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json;


namespace SharpCraft
{
    class MainMenu
    {
        MainGame game;
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        Dictionary<string, Texture2D> menuTextures;

        Rectangle background;
        Texture2D backgroundTexture;

        Rectangle logo;
        Texture2D logoTexture;

        Button resume, newGame, settings, quit, //main buttons
               back, renderDistance, worldType;  //settings buttons

        SpriteFont font14, font24;

        bool main, settingsMenu;

        KeyboardState previousKeyboardState;
        MouseState previousMouseState;


        public MainMenu(MainGame _game, GraphicsDeviceManager _graphics, Dictionary<string, Texture2D> _textures,
                        SpriteFont[] fonts)
        {
            game = _game;
            game.IsMouseVisible = true;

            graphics = _graphics;
            menuTextures = _textures;

            spriteBatch = new SpriteBatch(graphics.GraphicsDevice);

            background = new Rectangle(0, 0, game.Window.ClientBounds.Width, game.Window.ClientBounds.Height);
            backgroundTexture = menuTextures["menu_background"];

            logoTexture = menuTextures["logo"];
            logo = new Rectangle(100, 0, logoTexture.Width, logoTexture.Height);

            font14 = fonts[0];
            font24 = fonts[1];

            resume = new Button((game.Window.ClientBounds.Width / 2) - 200, 2 * 70, 400, 70,
                       menuTextures["button"], menuTextures["button_selector"], font24, "Resume");
            resume.SetShading(graphics);

            if (!File.Exists(@"Save\parameters.json"))
            {
                resume.Inactive = true;
            }

            newGame = new Button((game.Window.ClientBounds.Width / 2) - 200, 3 * 70, 400, 70,
                       menuTextures["button"], menuTextures["button_selector"], font24, "New Game");

            settings = new Button((game.Window.ClientBounds.Width / 2) - 200, 4 * 70, 400, 70,
                       menuTextures["button"], menuTextures["button_selector"], font24, "Settings");

            quit = new Button((game.Window.ClientBounds.Width / 2) - 200, 5 * 70, 400, 70,
                       menuTextures["button"], menuTextures["button_selector"], font24, "Quit Game");

            back = new Button((game.Window.ClientBounds.Width / 2) - 200, 4 * 70, 400, 70,
                       menuTextures["button"], menuTextures["button_selector"], font24, "Back");

            renderDistance = new Button((game.Window.ClientBounds.Width / 2) - 200, 70, 400, 70,
                       menuTextures["button"], menuTextures["button_selector"], font24, "Render Distance");

            worldType = new Button((game.Window.ClientBounds.Width / 2) - 200, 2 * 70, 400, 70,
                       menuTextures["button"], menuTextures["button_selector"], font24, "World Type");


            main = true;

            previousKeyboardState = Keyboard.GetState();
            previousMouseState = Mouse.GetState();

            LoadSettings();
        }

        public void Update()
        {
            MouseState currentMouseState = Mouse.GetState();
            KeyboardState currentKeyboardState = Keyboard.GetState();
            Point mouseLoc = new Point(currentMouseState.X, currentMouseState.Y);

            if (main)
            {
                MainControl(currentMouseState, mouseLoc);
            }
            else if (settingsMenu)
            {
                SettingsControl(currentMouseState, mouseLoc);
            }

            previousKeyboardState = currentKeyboardState;
            previousMouseState = currentMouseState;
        }

        public void Draw()
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            spriteBatch.Draw(backgroundTexture, background, Color.White);

            if (main)
            {
                spriteBatch.Draw(logoTexture, logo, Color.White);
                resume.Draw(spriteBatch);
                newGame.Draw(spriteBatch);
                settings.Draw(spriteBatch);
                quit.Draw(spriteBatch);
            }
            else if (settingsMenu)
            {
                renderDistance.Draw(spriteBatch, Parameters.RenderDistance);
                worldType.Draw(spriteBatch, Parameters.WorldType);
                back.Draw(spriteBatch);
            }

            spriteBatch.End();
        }

        public void DrawLoadingScreen()
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            spriteBatch.Draw(backgroundTexture, background, Color.White);

            Vector2 textSize = font24.MeasureString("Loading World");
            spriteBatch.DrawString(font24, "Loading World",
                (new Vector2(game.Window.ClientBounds.Width, game.Window.ClientBounds.Height - textSize.Y) - textSize) / 2, Color.White);

            spriteBatch.End();
        }

        void MainControl(MouseState currentMouseState, Point mouseLoc)
        {
            if (resume.Contains(mouseLoc))
            {
                resume.Selected = true;

                if (Util.LeftButtonClicked(currentMouseState, previousMouseState))
                {
                    game.IsMouseVisible = false;
                    Parameters.GameLoading = true;
                    LoadParameters();
                    return;
                }
            }

            if (newGame.Contains(mouseLoc))
            {
                newGame.Selected = true;

                if (Util.LeftButtonClicked(currentMouseState, previousMouseState))
                {
                    newGame.Selected = false;
                    game.IsMouseVisible = false;

                    Parameters.GameLoading = true;
                    Parameters.Position = Vector3.Zero;
                    Parameters.IsFlying = false;
                    Parameters.Direction = new Vector3(0, -0.5f, -1f);

                    var rnd = new Random();
                    Parameters.Seed = rnd.Next();

                    if (File.Exists(@"Save\save.db"))
                    {
                        string path = @"URI=file:" + Directory.GetCurrentDirectory() + @"\Save\save.db";

                        var connection = new SQLiteConnection(path);
                        connection.Open();

                        var cmd = new SQLiteCommand(connection)
                        {
                            CommandText = @"DROP TABLE chunks"
                        };
                        cmd.ExecuteNonQuery();
                    }

                    return;
                }
            }

            if (settings.Contains(mouseLoc))
            {
                settings.Selected = true;

                if (Util.LeftButtonClicked(currentMouseState, previousMouseState))
                {
                    settings.Selected = false;

                    main = false;
                    settingsMenu = true;
                }
            }

            if (quit.Contains(mouseLoc))
            {
                quit.Selected = true;

                if (Util.LeftButtonClicked(currentMouseState, previousMouseState))
                {
                    game.Exit();
                }
            }
        }

        void SettingsControl(MouseState currentMouseState, Point mouseLoc)
        {
            if (back.Contains(mouseLoc))
            {
                back.Selected = true;

                if (Util.LeftButtonClicked(currentMouseState, previousMouseState))
                {
                    back.Selected = false;

                    main = true;
                    settingsMenu = false;

                    List<Settings> data = new List<Settings>(1);
                    data.Add(new Settings()
                    {
                        renderDistance = Parameters.RenderDistance,
                        worldType = Parameters.WorldType
                    });

                    string json = JsonConvert.SerializeObject(data);
                    string path = Directory.GetCurrentDirectory() + @"\Save\settings.json";

                    File.WriteAllText(path, json);
                }
            }

            else if (renderDistance.Contains(mouseLoc))
            {
                renderDistance.Selected = true;

                if (Util.LeftButtonClicked(currentMouseState, previousMouseState))
                {
                    if (Parameters.RenderDistance < 16)
                        Parameters.RenderDistance++;
                    else
                        Parameters.RenderDistance = 1;
                }
                else if (Util.RightButtonClicked(currentMouseState, previousMouseState))
                {
                    if (Parameters.RenderDistance > 1)
                        Parameters.RenderDistance--;
                    else
                        Parameters.RenderDistance = 16;
                }
            }

            else if (worldType.Contains(mouseLoc))
            {
                worldType.Selected = true;

                if (Util.LeftButtonClicked(currentMouseState, previousMouseState) ||
                    Util.RightButtonClicked(currentMouseState, previousMouseState))
                {
                    if (Parameters.WorldType == "Default")
                        Parameters.WorldType = "Flat";
                    else
                        Parameters.WorldType = "Default";
                }
            }
        }

        void LoadSettings()
        {
            if (File.Exists(@"Save\settings.json"))
            {
                Settings data;
                using (StreamReader r = new StreamReader("Save/settings.json"))
                {
                    string json = r.ReadToEnd();
                    data = JsonConvert.DeserializeObject<List<Settings>>(json)[0];
                }

                Parameters.RenderDistance = data.renderDistance;
                Parameters.WorldType = data.worldType;
            }
        }

        void LoadParameters()
        {
            if (!File.Exists(@"Save\parameters.json"))
            {
                var rnd = new Random();
                Parameters.Seed = rnd.Next();
                return;
            }

            SaveParameters data;
            using (StreamReader r = new StreamReader("Save/parameters.json"))
            {
                string json = r.ReadToEnd();
                data = JsonConvert.DeserializeObject<List<SaveParameters>>(json)[0];
            }

            Parameters.Seed = data.seed;
            Parameters.IsFlying = data.isFlying;
            Parameters.Position = new Vector3(data.X, data.Y, data.Z);
            Parameters.Direction = new Vector3(data.dirX, data.dirY, data.dirZ);
            Parameters.Inventory = data.inventory;
            Parameters.WorldType = data.worldType;
        }
    }
}

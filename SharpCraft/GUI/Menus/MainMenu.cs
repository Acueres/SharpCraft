using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using SharpCraft.Utilities;
using SharpCraft.Assets;
using SharpCraft.GUI.Elements;
using SharpCraft.GUI.Components;
using SharpCraft.Persistence;

namespace SharpCraft.GUI.Menus
{
    class MainMenu
    {
        public Save CurrentSave { get; private set; }

        readonly MainGame game;
        GraphicsDevice graphics;
        SpriteBatch spriteBatch;

        Parameters newWorldParameters;

        readonly AssetServer assetServer;

        Rectangle background;
        Texture2D backgroundTexture;

        Rectangle logo;
        Texture2D logoTexture;

        Dictionary<string, GUIElement>
        mainLayout,
        newWorldLayout,
        loadWorldLayout,
        settingsLayout;

        TextBox worldName;

        Label saving, loading;

        SpriteFont font14, font24;

        MenuState state;

        enum MenuState
        {
            Main,
            Settings,
            LoadWorld,
            NewWorld
        }

        KeyboardState previousKeyboardState;
        MouseState previousMouseState;

        readonly int screenWidth;
        readonly int screenHeight;

        readonly List<Save> saves;
        SaveGrid saveGrid;


        public MainMenu(MainGame game, int screenWidth, int screenHeight, GraphicsDevice graphics, AssetServer assetServer)
        {
            this.game = game;
            game.IsMouseVisible = true;
            this.graphics = graphics;
            spriteBatch = new SpriteBatch(graphics);
            this.assetServer = assetServer;

            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;

            font14 = assetServer.GetFont(0);
            font24 = assetServer.GetFont(1);

            state = MenuState.Main;

            previousKeyboardState = Keyboard.GetState();
            previousMouseState = Mouse.GetState();

            saves = Save.LoadAll(graphics);

            InitializeGUI();
        }

        public void Update()
        {
            MouseState currentMouseState = Mouse.GetState();
            KeyboardState currentKeyboardState = Keyboard.GetState();
            Point mouseLoc = new(currentMouseState.X, currentMouseState.Y);

            switch (state)
            {
                case MenuState.Main:
                    {
                        MainControl(currentMouseState, mouseLoc);
                        break;
                    }

                case MenuState.Settings:
                    {
                        SettingsControl(currentMouseState, mouseLoc);
                        break;
                    }

                case MenuState.LoadWorld:
                    {
                        LoadWorldControl(currentKeyboardState, currentMouseState, mouseLoc);
                        break;
                    }

                case MenuState.NewWorld:
                    {
                        NewWorldControl(currentKeyboardState, currentMouseState, mouseLoc);
                        break;
                    }
            }

            previousKeyboardState = currentKeyboardState;
            previousMouseState = currentMouseState;
        }

        public void Draw()
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            spriteBatch.Draw(backgroundTexture, background, Color.White);

            switch (state)
            {
                case MenuState.Main:
                    {
                        spriteBatch.Draw(logoTexture, logo, Color.White);

                        foreach (var element in mainLayout.Values)
                        {
                            element.Draw();
                        }
                        break;
                    }

                case MenuState.Settings:
                    {
                        foreach (var element in settingsLayout.Values)
                        {
                            element.Draw();
                        }

                        settingsLayout["Render Distance"].Draw(Settings.RenderDistance.ToString());
                        break;
                    }

                case MenuState.NewWorld:
                    {
                        worldName.Draw();

                        foreach (var element in newWorldLayout.Values)
                        {
                            element.Draw();
                        }

                        newWorldLayout["World Type"].Draw(newWorldParameters.WorldType);
                        break;
                    }

                case MenuState.LoadWorld:
                    {
                        saveGrid.Draw();

                        foreach (var element in loadWorldLayout.Values)
                        {
                            element.Draw();
                        }
                        break;
                    }
            }

            spriteBatch.End();
        }

        public void DrawLoadingScreen()
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            spriteBatch.Draw(backgroundTexture, background, Color.White);
            loading.Draw();

            spriteBatch.End();
        }

        public void DrawSavingScreen()
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            spriteBatch.Draw(backgroundTexture, background, Color.White);
            saving.Draw();

            spriteBatch.End();
        }

        void MainControl(MouseState currentMouseState, Point mouseLoc)
        {
            bool leftClick = Util.LeftButtonClicked(currentMouseState, previousMouseState);

            foreach (var element in mainLayout.Values)
            {
                element.Update(mouseLoc, leftClick);
            }
        }

        void SettingsControl(MouseState currentMouseState, Point mouseLoc)
        {
            bool leftClick = Util.LeftButtonClicked(currentMouseState, previousMouseState);
            bool rightClick = Util.RightButtonClicked(currentMouseState, previousMouseState);

            foreach (var element in settingsLayout.Values)
            {
                element.Update(mouseLoc, leftClick);
            }

            if (settingsLayout["Render Distance"].Clicked(mouseLoc, leftClick))
            {
                if (Settings.RenderDistance < 16)
                    Settings.RenderDistance++;
                else
                    Settings.RenderDistance = 1;
            }
            else if (settingsLayout["Render Distance"].Clicked(mouseLoc, rightClick))
            {
                if (Settings.RenderDistance > 1)
                    Settings.RenderDistance--;
                else
                    Settings.RenderDistance = 16;
            }
        }

        void NewWorldControl(KeyboardState currentKeyboardState, MouseState currentMouseState, Point mouseLoc)
        {
            bool leftClick = Util.LeftButtonClicked(currentMouseState, previousMouseState);
            bool rightClick = Util.RightButtonClicked(currentMouseState, previousMouseState);

            newWorldLayout["Create World"].Inactive = saves.Exists(x =>
            x.Name == worldName.ToString());

            worldName.Update(mouseLoc, currentKeyboardState, previousKeyboardState, leftClick, rightClick);

            foreach (var element in newWorldLayout.Values)
            {
                element.Update(mouseLoc, leftClick);
            }
        }

        void LoadWorldControl(KeyboardState currentKeyboardState, MouseState currentMouseState, Point mouseLoc)
        {
            bool leftClick = Util.LeftButtonClicked(currentMouseState, previousMouseState);

            loadWorldLayout["Play"].Inactive = saves.Count == 0;
            loadWorldLayout["Reset"].Inactive = saves.Count == 0;
            loadWorldLayout["Delete"].Inactive = saves.Count == 0;

            foreach (var element in loadWorldLayout.Values)
            {
                element.Update(mouseLoc, leftClick);
            }

            saveGrid.Update(currentMouseState, previousMouseState, mouseLoc);
        }

        void InitializeGUI()
        {
            const int elementWidth = 300;
            const int elementHeight = 40;
            int offset = elementHeight + screenWidth / elementHeight;

            background = new Rectangle(0, 0, screenWidth, screenHeight);
            backgroundTexture = assetServer.GetMenuTexture("menu_background");

            logoTexture = assetServer.GetMenuTexture("logo");
            logo = new Rectangle(100, 0, logoTexture.Width, logoTexture.Height);

            saving = new Label(spriteBatch, "Saving World", font24,
                (new Vector2(screenWidth, screenHeight) - font24.MeasureString("Saving World")) / 2, Color.White);

            loading = new Label(spriteBatch, "Loading World", font24,
                (new Vector2(screenWidth, screenHeight) - font24.MeasureString("Loading World")) / 2, Color.White);


            mainLayout = new Dictionary<string, GUIElement>()
            {
                ["New World"] = new Button(graphics, spriteBatch, "New World", font14,
                screenWidth / 2 - elementWidth / 2, 3 * offset, elementWidth, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    newWorldParameters = new Parameters();
                    worldName.Clear();
                    state = MenuState.NewWorld;
                }),

                ["Load World"] = new Button(graphics, spriteBatch, "Load World", font14,
                screenWidth / 2 - elementWidth / 2, 4 * offset, elementWidth, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    saves.Sort((a, b) => a.Parameters.Date.CompareTo(b.Parameters.Date));
                    saves.Reverse();

                    state = MenuState.LoadWorld;
                }),

                ["Settings"] = new Button(graphics, spriteBatch, "Settings", font14,
                screenWidth / 2 - elementWidth / 2, 5 * offset, elementWidth, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    state = MenuState.Settings;
                }),

                ["Quit"] = new Button(graphics, spriteBatch, "Quit Game", font14,
                screenWidth / 2 - elementWidth / 2, 6 * offset, elementWidth, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    game.Exit();
                })
            };


            settingsLayout = new Dictionary<string, GUIElement>()
            {
                ["Render Distance"] = new Button(graphics, spriteBatch, "Render Distance: ", font14,
                screenWidth / 2 - elementWidth / 2, offset, elementWidth, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector")),

                ["Back"] = new Button(graphics, spriteBatch, "Back", font14,
                screenWidth - elementWidth, screenHeight - elementHeight, elementWidth, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    state = MenuState.Main;
                    Settings.Save();
                })
            };


            newWorldLayout = new Dictionary<string, GUIElement>()
            {
                ["Create World Label"] = new Label(spriteBatch, "Create New World", font24,
                new Vector2(screenWidth / 2 - 130, 0), Color.White),

                ["World Name Label"] = new Label(spriteBatch, "World Name", font14,
                new Vector2(screenWidth / 2 - 200, 100), Color.White),

                ["World Type"] = new Button(graphics, spriteBatch, "World Type: ", font14,
                screenWidth / 2 - elementWidth / 2, 3 * offset, elementWidth, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    if (newWorldParameters.WorldType == "Default")
                    {
                        newWorldParameters.WorldType = "Flat";
                    }
                    else
                    {
                        newWorldParameters.WorldType = "Default";
                    }
                }),

                ["Create World"] = new Button(graphics, spriteBatch, "Create World", font14,
                0, screenHeight - elementHeight, elementWidth, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    string saveName = worldName.ToString();

                    CurrentSave = new Save(saveName, newWorldParameters);

                    saves.Add(CurrentSave);

                    Directory.CreateDirectory($@"Saves\{saveName}");

                    state = MenuState.Main;
                    game.IsMouseVisible = false;
                    game.State = GameState.Loading;
                }),

                ["Cancel"] = new Button(graphics, spriteBatch, "Cancel", font14,
                screenWidth - elementWidth, screenHeight - elementHeight, elementWidth, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    state = MenuState.Main;
                })
            };

            worldName = new TextBox(game.Window, graphics, spriteBatch,
                (screenWidth - 400) / 2, 2 * offset, 400, elementHeight,
                assetServer.GetMenuTexture("button_selector"), font14);


            loadWorldLayout = new Dictionary<string, GUIElement>()
            {
                ["Select World"] = new Label(spriteBatch, "Select World", font24,
                new Vector2(screenWidth / 2 - 90, 0), Color.White),

                ["Play"] = new Button(graphics, spriteBatch, "Play", font14,
                0, screenHeight - elementHeight, elementWidth / 2, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    CurrentSave = saveGrid.SelectedSave;
                    state = MenuState.Main;
                    game.IsMouseVisible = false;
                    game.State = GameState.Loading;
                }),

                ["Reset"] = new Button(graphics, spriteBatch, "Reset", font14,
                400, screenHeight - elementHeight, elementWidth / 2, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    saveGrid.SelectedSave.Clear();
                }),


                ["Delete"] = new Button(graphics, spriteBatch, "Delete", font14,
                400 - elementWidth / 2, screenHeight - elementHeight, elementWidth / 2, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    Save toDelete = saveGrid.SelectedSave;
                    Directory.Delete($@"Saves\{toDelete.Name}", true);
                    saves.Remove(toDelete);
                }),

                ["Cancel"] = new Button(graphics, spriteBatch, "Cancel", font14,
                screenWidth - elementWidth / 2, screenHeight - elementHeight, elementWidth / 2, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    state = MenuState.Main;
                })
            };

            saveGrid = new SaveGrid(graphics, spriteBatch, assetServer, screenWidth,
                elementWidth, elementHeight, saves);
        }
    }
}

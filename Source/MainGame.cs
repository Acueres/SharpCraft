using System;
using System.IO;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    static class GameState
    {
        public static bool
        Loading = false,
        Started = false,
        Paused = false,
        ExitedGameMenu = false,
        ExitingToMainMenu = false;
    }

    public class MainGame : Game
    {
        GraphicsDeviceManager graphics;

        Player player;
        World world;
        GameMenu gameMenu;
        MainMenu mainMenu;
        Renderer renderer;
        DatabaseHandler saveHandler;
        Save currentSave;
        Time time;


        public MainGame()
        {
            graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 854,
                PreferredBackBufferHeight = 480,
                IsFullScreen = false
            };

            IsFixedTimeStep = true;

            Content.RootDirectory = "Assets";
        }

        protected override void Initialize()
        {
            Settings.Load();

            Assets.Load(Content);

            mainMenu = new MainMenu(this, graphics);

            base.Initialize();
        }

        protected override void UnloadContent()
        {
            Content.Unload();
        }

        protected override void Update(GameTime gameTime)
        {
            if (IsActive)
            {
                if (GameState.Started)
                {
                    if (!GameState.Paused)
                    {
                        player.Update(gameTime);
                        world.Update();
                    }

                    gameMenu.Update();
                }
                else if (GameState.Loading)
                {
                    NewGame(gameTime);
                }
                else if (GameState.ExitingToMainMenu)
                {
                    saveHandler.Close();

                    player.SaveParameters(currentSave.Parameters);
                    time.SaveParameters(currentSave.Parameters);
                    currentSave.Parameters.Save();

                    player = null;
                    world = null;
                    saveHandler = null;
                    gameMenu = null;
                    renderer = null;

                    GC.Collect();

                    GameState.ExitingToMainMenu = false;
                    IsMouseVisible = true;
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
            if (GameState.Started)
            {
                renderer.Draw(world.ActiveChunks, player);
                gameMenu.Draw((int)Math.Round(1 / gameTime.ElapsedGameTime.TotalSeconds));
            }
            else if (GameState.Loading)
            {
                mainMenu.DrawLoadingScreen();
            }
            else if (GameState.ExitingToMainMenu)
            {
                mainMenu.DrawSavingScreen();
            }
            else
            {
                mainMenu.Draw();
            }

            base.Draw(gameTime);
        }

        void NewGame(GameTime gameTime)
        {
            GameState.Loading = false;
            GameState.Started = true;

            currentSave = mainMenu.CurrentSave;

            time = new Time(currentSave.Parameters.Day, currentSave.Parameters.Hour, currentSave.Parameters.Minute);

            saveHandler = new DatabaseHandler(currentSave.Parameters.SaveName);
            gameMenu = new GameMenu(this, graphics, time, currentSave.Parameters);
            world = new World(gameMenu, saveHandler, currentSave.Parameters);
            player = new Player(graphics, currentSave.Parameters);
            renderer = new Renderer(graphics, time, world.Region, currentSave.Parameters);

            world.SetPlayer(player, currentSave.Parameters);

            if (!File.Exists($@"Saves\{currentSave.Parameters.SaveName}\save_icon.png"))
            {
                player.Update(gameTime);
                world.Update();
                renderer.Draw(world.ActiveChunks, player);
                currentSave.Icon = Util.Screenshot(graphics.GraphicsDevice,
                    Window.ClientBounds.Width, Window.ClientBounds.Height,
                    $@"Saves\{currentSave.Parameters.SaveName}\save_icon.png");
            }
        }
    }
}

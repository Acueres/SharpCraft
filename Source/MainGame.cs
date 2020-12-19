using System;
using System.IO;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    public enum GameState
    {
        Loading,
        Started,
        MainMenu,
        Exiting
    }

    public class MainGame : Game
    {
        public GameState State;
        public bool Paused;
        public bool ExitedMenu;

        GraphicsDeviceManager graphics;

        Player player;
        World world;
        GameMenu gameMenu;
        MainMenu mainMenu;
        Renderer renderer;
        DatabaseHandler databaseHandler;
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
            State = GameState.MainMenu;
            Paused = false;
            ExitedMenu = false;

            Settings.Load();

            Assets.Load(Content);

            mainMenu = new MainMenu(this, GraphicsDevice);

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
                switch (State)
                {
                    case GameState.Started:
                        {
                            if (!Paused)
                            {
                                player.Update(gameTime);
                                world.Update();
                            }

                            gameMenu.Update();

                            break;
                        }

                    case GameState.Loading:
                        {
                            State = GameState.Started;

                            currentSave = mainMenu.CurrentSave;

                            time = new Time(currentSave.Parameters.Day, currentSave.Parameters.Hour, currentSave.Parameters.Minute);

                            ScreenshotTaker screenshotTaker = new ScreenshotTaker(GraphicsDevice, Window.ClientBounds.Width,
                                                                                  Window.ClientBounds.Height);
                            BlockSelector blockSelector = new BlockSelector(GraphicsDevice);

                            databaseHandler = new DatabaseHandler(this, currentSave.Parameters.SaveName);
                            gameMenu = new GameMenu(this, GraphicsDevice, time, screenshotTaker, currentSave.Parameters);
                            world = new World(gameMenu, databaseHandler, blockSelector, currentSave.Parameters);
                            player = new Player(this, GraphicsDevice, currentSave.Parameters);
                            renderer = new Renderer(this, GraphicsDevice, time, world.Region, screenshotTaker, blockSelector);

                            world.SetPlayer(this, player, currentSave.Parameters);

                            if (!File.Exists($@"Saves\{currentSave.Parameters.SaveName}\save_icon.png"))
                            {
                                player.Update(gameTime);
                                world.Update();
                                renderer.Draw(world.ActiveChunks, player);
                                screenshotTaker.SaveIcon(currentSave.Parameters.SaveName, out currentSave.Icon);
                            }

                            break;
                        }

                    case GameState.Exiting:
                        {
                            databaseHandler.Close();

                            player.SaveParameters(currentSave.Parameters);
                            time.SaveParameters(currentSave.Parameters);
                            currentSave.Parameters.Save();

                            player = null;
                            world = null;
                            databaseHandler = null;
                            gameMenu = null;
                            renderer = null;

                            GC.Collect();

                            State = GameState.MainMenu;
                            IsMouseVisible = true;

                            break;
                        }

                    case GameState.MainMenu:
                        {
                            mainMenu.Update();
                            break;
                        }
                }
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            switch (State)
            {
                case GameState.Started:
                    {
                        renderer.Draw(world.ActiveChunks, player);
                        gameMenu.Draw((int)Math.Round(1 / gameTime.ElapsedGameTime.TotalSeconds));
                        break;
                    }

                case GameState.Loading:
                    {
                        mainMenu.DrawLoadingScreen();
                        break;
                    }

                case GameState.Exiting:
                    {
                        mainMenu.DrawSavingScreen();
                        break;
                    }

                case GameState.MainMenu:
                    {
                        mainMenu.Draw();
                        break;
                    }
            }

            base.Draw(gameTime);
        }
    }
}

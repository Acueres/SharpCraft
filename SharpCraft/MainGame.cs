﻿using System;
using System.IO;

using Microsoft.Xna.Framework;

using SharpCraft.Handlers;
using SharpCraft.Menu;
using SharpCraft.World;
using SharpCraft.Utility;
using SharpCraft.Assets;

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

        const string assetDirectory = "Assets";

        Player player;
        readonly AssetServer assetServer;
        readonly BlockMetadataProvider blockMetadata;
        WorldSystem world;
        GameMenu gameMenu;
        MainMenu mainMenu;
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

            Content.RootDirectory = assetDirectory;

            blockMetadata = new BlockMetadataProvider(assetDirectory);
            assetServer = new AssetServer(Content, assetDirectory);
        }

        protected override void Initialize()
        {
            State = GameState.MainMenu;
            Paused = false;
            ExitedMenu = false;

            Settings.Load();

            blockMetadata.Load();
            assetServer.Load(GraphicsDevice);

            mainMenu = new MainMenu(this, GraphicsDevice, assetServer);

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
                                if (player.UpdateOccured)
                                {
                                    world.UpdateBlocks();
                                    world.Update();
                                }
                            }

                            gameMenu.Update();

                            break;
                        }

                    case GameState.Loading:
                        {
                            State = GameState.Started;

                            currentSave = mainMenu.CurrentSave;

                            time = new Time(currentSave.Parameters.Day, currentSave.Parameters.Hour, currentSave.Parameters.Minute);

                            ScreenshotHandler screenshotHandler = new(GraphicsDevice, Window.ClientBounds.Width,
                                                                                  Window.ClientBounds.Height);
                            BlockSelector blockSelector = new(GraphicsDevice, assetServer);

                            databaseHandler = new DatabaseHandler(this, currentSave.Parameters.SaveName, blockMetadata);
                            player = new Player(this, GraphicsDevice, currentSave.Parameters);
                            gameMenu = new GameMenu(this, GraphicsDevice, time, screenshotHandler, currentSave.Parameters, assetServer, blockMetadata, player);
                            world = new WorldSystem(gameMenu, databaseHandler, blockSelector, currentSave.Parameters, blockMetadata, time, assetServer, GraphicsDevice, screenshotHandler);

                            world.SetPlayer(this, player, currentSave.Parameters);

                            if (!File.Exists($@"Saves\{currentSave.Parameters.SaveName}\save_icon.png"))
                            {
                                player.Update(gameTime);
                                world.Update();
                                world.Render();
                                screenshotHandler.SaveIcon(currentSave.Parameters.SaveName, out currentSave.Icon);
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
                        world.Render();
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

﻿using System;
using System.IO;
using Microsoft.Xna.Framework;

using SharpCraft.World;
using SharpCraft.Utilities;
using SharpCraft.Assets;
using SharpCraft.World.Blocks;
using SharpCraft.GUI.Menus;
using SharpCraft.Persistence;
using SharpCraft.Rendering;
using SharpCraft.Rendering.Meshers;
using SharpCraft.World.Light;
using SharpCraft.World.Chunks;

namespace SharpCraft
{
    public enum GameState
    {
        Loading,
        Running,
        MainMenu,
        Exiting
    }

    public class MainGame : Game
    {
        public GameState State { get; set; }
        public bool Paused { get; set; }
        public bool ExitedMenu { get; set; }

        GraphicsDeviceManager graphics;

        const string assetDirectory = "Assets";

        Player player;
        readonly AssetServer assetServer;
        readonly BlockMetadataProvider blockMetadata;
        WorldSystem world;
        Renderer renderer;
        GameMenu gameMenu;
        MainMenu mainMenu;
        DatabaseService db;
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

            mainMenu = new MainMenu(this, graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight, GraphicsDevice, assetServer);

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
                    case GameState.Running:
                        {
                            if (!Paused)
                            {
                                world.Update(gameTime, ExitedMenu);
                            }

                            gameMenu.Update();

                            break;
                        }

                    case GameState.Loading:
                        {
                            State = GameState.Running;

                            currentSave = mainMenu.CurrentSave;

                            time = new Time(currentSave.Parameters.Day, currentSave.Parameters.Hour, currentSave.Parameters.Minute);

                            ScreenshotTaker screenshotTaker = new(GraphicsDevice, Window.ClientBounds.Width,
                                                                                  Window.ClientBounds.Height);
                            BlockSelector blockSelector = new(GraphicsDevice, assetServer);

                            db = new DatabaseService(this, currentSave.Parameters.SaveName, blockMetadata);
                            db.Initialize();

                            AdjacencyGraph adjacencyGraph = new();
                            LightSystem lightSystem = new(blockMetadata, adjacencyGraph);
                            ChunkMesher chunkMesher = new(blockMetadata, lightSystem);

                            player = new Player(GraphicsDevice, currentSave.Parameters);
                            gameMenu = new GameMenu(this, GraphicsDevice, time, screenshotTaker, currentSave.Parameters, assetServer, blockMetadata, player);
                            world = new WorldSystem(gameMenu, db, lightSystem, blockSelector, currentSave.Parameters, blockMetadata, adjacencyGraph, chunkMesher);
                            renderer = new Renderer(graphics.GraphicsDevice, blockSelector, assetServer, blockMetadata, screenshotTaker, chunkMesher);

                            world.SetPlayer(player, currentSave.Parameters);

                            if (!File.Exists($@"Saves\{currentSave.Parameters.SaveName}\save_icon.png"))
                            {
                                player.Update(gameTime);
                                world.Init();
                                renderer.Render(world.GetActiveChunks(), player.Camera, time);
                                screenshotTaker.SaveIcon(currentSave.Parameters.SaveName, out currentSave.Icon);
                            }

                            break;
                        }

                    case GameState.Exiting:
                        {
                            db.Close();

                            player.SaveParameters(currentSave.Parameters);
                            time.SaveParameters(currentSave.Parameters);
                            currentSave.Parameters.Save();

                            player = null;
                            world = null;
                            db = null;
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
                case GameState.Running:
                    {
                        renderer.Render(world.GetActiveChunks(), player.Camera, time);
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

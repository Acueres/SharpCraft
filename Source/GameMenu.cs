﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpCraft.GUI;


namespace SharpCraft
{
    class GameMenu
    {
        public ushort? SelectedItem
        {
            get
            {
                return inventory.SelectedItem;
            }
        }

        MainGame game;
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Inventory inventory;
        Time time;

        Rectangle crosshair;

        Rectangle screenShading;
        Texture2D screenShadingTexture;

        Button back, quit;

        SpriteFont font14, font24;

        MenuState state;

        enum MenuState
        {
            Main,
            Pause,
            Inventory
        }

        bool debugScreen;

        KeyboardState previousKeyboardState;
        MouseState previousMouseState;

        ReadOnlyDictionary<string, Texture2D> menuTextures;

        int screenWidth, screenHeight;
        Vector2 screenCenter;


        public GameMenu(MainGame _game, GraphicsDeviceManager _graphics, Time _time, Parameters parameters)
        {
            game = _game;
            graphics = _graphics;
            spriteBatch = new SpriteBatch(graphics.GraphicsDevice);

            time = _time;

            menuTextures = Assets.MenuTextures;

            state = MenuState.Main;
            debugScreen = false;

            font14 = Assets.Fonts[0];
            font24 = Assets.Fonts[1];

            inventory = new Inventory(game, spriteBatch, font14, parameters, () =>
            {
                Mouse.SetPosition((int)screenCenter.X, (int)screenCenter.Y);
                game.IsMouseVisible = false;
                game.Paused = false;
                game.ExitedMenu = true;
                state = MenuState.Main;
            });

            previousKeyboardState = Keyboard.GetState();
            previousMouseState = Mouse.GetState();

            screenWidth = game.Window.ClientBounds.Width;
            screenHeight = game.Window.ClientBounds.Height;

            screenCenter = new Vector2(screenWidth / 2, screenHeight / 2);


            back = new Button(graphics, spriteBatch, "Back to game", font14,
                (screenWidth / 2) - 150, 100, 300, 40, 
                menuTextures["button"], menuTextures["button_selector"], () => 
                {
                    game.Paused = false;

                    Mouse.SetPosition((int)screenCenter.X, (int)screenCenter.Y);
                    game.IsMouseVisible = false;
                    game.ExitedMenu = true;

                    state = MenuState.Main;
                });

            quit = new Button(graphics, spriteBatch, "Save & Quit", font14,
                (screenWidth / 2) - 150, 260, 300, 40,
                menuTextures["button"], menuTextures["button_selector"], () => 
                {
                    game.Paused = false;
                    game.IsMouseVisible = false;
                    inventory.SaveParameters(parameters);
                    game.State = GameState.Exiting;
                });


            crosshair = new Rectangle((screenWidth / 2) - 15, (screenHeight / 2) - 15, 30, 30);

            screenShading = new Rectangle(0, 0, screenWidth, screenHeight);
            screenShadingTexture = new Texture2D(graphics.GraphicsDevice, screenWidth, screenHeight);

            Color[] darkBackGroundColor = new Color[screenWidth * game.Window.ClientBounds.Height];
            for (int i = 0; i < darkBackGroundColor.Length; i++)
                darkBackGroundColor[i] = new Color(Color.Black, 0.5f);

            screenShadingTexture.SetData(darkBackGroundColor);
        }

        public void Update()
        {
            MouseState currentMouseState = Mouse.GetState();
            KeyboardState currentKeyboardState = Keyboard.GetState();
            Point mouseLoc = new Point(currentMouseState.X, currentMouseState.Y);

            game.ExitedMenu = false;

            switch (state)
            {
                case MenuState.Main:
                    {
                        MainControl(currentKeyboardState);
                        inventory.UpdateHotbar(currentMouseState);
                        break;
                    }

                case MenuState.Pause:
                    {
                        PauseControl(currentKeyboardState, currentMouseState, mouseLoc);
                        break;
                    }

                case MenuState.Inventory:
                    {
                        inventory.Update(currentKeyboardState, previousKeyboardState,
                            currentMouseState, previousMouseState, mouseLoc);
                        break;
                    }
            }

            previousKeyboardState = currentKeyboardState;
            previousMouseState = currentMouseState;
        }

        public void Draw(int fps)
        {
            MouseState currentMouseState = Mouse.GetState();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            inventory.DrawHotbar();

            if (game.Paused)
            {
                spriteBatch.Draw(screenShadingTexture, screenShading, Color.White);
            }

            switch (state)
            {
                case MenuState.Main:
                    {
                        spriteBatch.Draw(menuTextures["crosshair"], crosshair, Color.White);
                        break;
                    }

                case MenuState.Pause:
                    {
                        back.Draw();
                        quit.Draw();
                        break;
                    }

                case MenuState.Inventory:
                    {
                        inventory.Draw(currentMouseState);
                        break;
                    }
            }

            if (debugScreen)
            {
                DrawDebugScreen(fps);
            }

            spriteBatch.End();

            graphics.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        void MainControl(KeyboardState currentKeyboardState)
        {
            if (Util.KeyPressed(Keys.Escape, currentKeyboardState, previousKeyboardState))
            {
                game.Paused = true;

                Mouse.SetPosition((int)screenCenter.X, (int)screenCenter.Y);
                game.IsMouseVisible = true;

                state = MenuState.Pause;
            }

            else if (Util.KeyPressed(Keys.E, currentKeyboardState, previousKeyboardState))
            {
                game.Paused = true;

                Mouse.SetPosition((int)screenCenter.X, (int)screenCenter.Y);
                game.IsMouseVisible = true;

                state = MenuState.Inventory;
            }

            else if (Util.KeyPressed(Keys.F3, currentKeyboardState, previousKeyboardState))
            {
                debugScreen ^= true;
            }
        }

        void PauseControl(KeyboardState currentKeyboardState, MouseState currentMouseState, Point mouseLoc)
        {
            bool leftClick = Util.LeftButtonClicked(currentMouseState, previousMouseState);

            if (Util.KeyPressed(Keys.Escape, currentKeyboardState, previousKeyboardState))
            {
                game.Paused = false;

                Mouse.SetPosition((int)screenCenter.X, (int)screenCenter.Y);
                game.IsMouseVisible = false;

                state = MenuState.Main;
            }

            back.Update(mouseLoc, leftClick);
            quit.Update(mouseLoc, leftClick);
        }

        void DrawDebugScreen(int fps)
        {
            spriteBatch.DrawString(font14, "FPS: " + fps.ToString(), new Vector2(10, 10), Color.White);
            spriteBatch.DrawString(font14, $"Memory: {GC.GetTotalMemory(false) / (1024 * 1024)} Mb",
                new Vector2(10, 30), Color.White);
            spriteBatch.DrawString(font14, time.ToString(), new Vector2(10, 50), Color.White);
        }
    }
}

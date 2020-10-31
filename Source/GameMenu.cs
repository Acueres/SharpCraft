using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;


namespace SharpCraft
{
    class GameMenu
    {
        public ushort? ActiveTool;

        MainGame game;
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Rectangle crosshair,
                  selector,
                  inventory,
                  toolbar,
                  tool;

        Rectangle screenShading;
        Texture2D screenShadingTexture;

        Texture2D blackTexture;

        Button back, quit;

        SpriteFont font14, font24;

        int activeToolIndex;

        ushort?[] tools;
        ushort?[][] items;

        ushort? draggedTexture;
        ushort? hoveredTexture;

        bool menuActive;
        bool inventoryActive;

        int previousScrollValue;
        KeyboardState previousKeyboardState;
        MouseState previousMouseState;

        Dictionary<string, Texture2D> menuTextures;
        Texture2D[] blockTextures;
        Dictionary<ushort, string> blockNames;


        public GameMenu(MainGame _game, GraphicsDeviceManager _graphics,
                        Dictionary<string, Texture2D> _textures, Texture2D[] _blockTextures,
                        Dictionary<ushort, string> _blockNames, SpriteFont[] fonts)
        {
            game = _game;
            graphics = _graphics;

            spriteBatch = new SpriteBatch(graphics.GraphicsDevice);

            menuTextures = _textures;
            blockNames = _blockNames;

            blockTextures = _blockTextures;

            menuActive = false;
            inventoryActive = false;

            activeToolIndex = 0;

            tools = new ushort?[9];

            foreach (var i in Parameters.Inventory)
            {
                if (i != null)
                {
                    tools = Parameters.Inventory;
                    break;
                }
            }

            items = new ushort?[5][];

            for (int i = 0; i < items.Length; i++)
            {
                items[i] = new ushort?[9];
            }

            int row = 0, col = 0;
            foreach (var blockName in blockNames)
            {
                items[col][row] = blockName.Key;
                row++;

                if (row % 9 == 0)
                {
                    row = 0;
                    col++;
                }
            }

            draggedTexture = null;
            hoveredTexture = null;

            ActiveTool = tools[activeToolIndex];

            previousScrollValue = 0;
            previousKeyboardState = Keyboard.GetState();
            previousMouseState = Mouse.GetState();

            font14 = fonts[0];
            font24 = fonts[1];

            back = new Button((game.Window.ClientBounds.Width / 2) - 200, 70, 400, 70, 
                menuTextures["button"], menuTextures["button_selector"], font24, "Back to game");

            quit = new Button((game.Window.ClientBounds.Width / 2) - 200, 4 * 70, 400, 70,
                menuTextures["button"], menuTextures["button_selector"], font24, "Save & Quit");

            crosshair = new Rectangle((game.Window.ClientBounds.Width / 2) - 15, 
                (game.Window.ClientBounds.Height / 2) - 15, 30, 30);

            selector = new Rectangle((game.Window.ClientBounds.Width / 2) - 225, 
                game.Window.ClientBounds.Height - 50, 50, 50);

            inventory = new Rectangle((game.Window.ClientBounds.Width / 5), 30, 700, 700);

            toolbar = new Rectangle((game.Window.ClientBounds.Width / 2) - 225, 
                game.Window.ClientBounds.Height - 50, 450, 50);

            tool = new Rectangle((game.Window.ClientBounds.Width / 2) - 220, 
                game.Window.ClientBounds.Height - 45, 40, 40);

            screenShading = new Rectangle(0, 0, game.Window.ClientBounds.Width, 
                game.Window.ClientBounds.Height);
            screenShadingTexture = new Texture2D(graphics.GraphicsDevice, 
                game.Window.ClientBounds.Width, game.Window.ClientBounds.Height);

            Color[] darkBackGroundColor = new Color[game.Window.ClientBounds.Width * game.Window.ClientBounds.Height];
            for (int i = 0; i < darkBackGroundColor.Length; i++)
                darkBackGroundColor[i] = new Color(Color.Black, 0.5f);

            screenShadingTexture.SetData(darkBackGroundColor);

            blackTexture = new Texture2D(graphics.GraphicsDevice, 20, 10);
            Color[] blackTextureColor = new Color[200];
            for (int i = 0; i < blackTextureColor.Length; i++)
                blackTextureColor[i] = Color.Black;

            blackTexture.SetData(blackTextureColor);
        }

        public void Update()
        {
            MouseState currentMouseState = Mouse.GetState();
            KeyboardState currentKeyboardState = Keyboard.GetState();

            Parameters.ExitedGameMenu = false;

            if (!inventoryActive && Util.KeyPressed(Keys.Escape, currentKeyboardState, previousKeyboardState))
            {
                MenuSwitch();
            }

            if (!menuActive && Util.KeyPressed(Keys.E, currentKeyboardState, previousKeyboardState))
            {
                InventorySwitch();
            }

            else if (inventoryActive && (Util.KeyPressed(Keys.E, currentKeyboardState, previousKeyboardState) ||
                Util.KeyPressed(Keys.Escape, currentKeyboardState, previousKeyboardState)))
            {
                InventorySwitch();
            }

            if (!menuActive && !inventoryActive)
            {
                ActiveToolSwitch(currentMouseState);
            }

            else
            {
                Point mouseLoc = new Point(currentMouseState.X, currentMouseState.Y);

                if (menuActive)
                {
                    MenuControl(currentMouseState, mouseLoc);
                }
                //Inventory control
                else if (inventoryActive)
                {
                    InventoryControl(currentMouseState, mouseLoc);
                }
            }

            previousKeyboardState = currentKeyboardState;
            previousMouseState = currentMouseState;
            previousScrollValue = currentMouseState.ScrollWheelValue;
        }

        public void Draw(int fps)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            spriteBatch.Draw(menuTextures["toolbar"], toolbar, new Color(Color.DarkGray, 0.8f));

            //Drawing active tools
            for (int i = 0; i < tools.Length; i++)
            {
                tool.X = (game.Window.ClientBounds.Width / 2) - 220 + (i * 50);

                if (tools[i] != null)
                {
                    spriteBatch.Draw(blockTextures[(ushort)tools[i]], tool, Color.White);
                }
            }

            //Draw selected tool name
            if (tools[activeToolIndex] != null)
            {
                Vector2 textSize = font14.MeasureString(blockNames[(ushort)tools[activeToolIndex]]);

                spriteBatch.Draw(blackTexture,
                    new Rectangle(toolbar.X, toolbar.Y - 20, (int)textSize.X, (int)textSize.Y), Color.Black);

                spriteBatch.DrawString(font14, blockNames[(ushort)tools[activeToolIndex]],
                    new Vector2(toolbar.X, toolbar.Y - 20f), Color.White);
            }

            spriteBatch.Draw(menuTextures["selector"], selector, Color.White);

            //Dim the background when menu is open
            if (Parameters.GamePaused)
            {
                spriteBatch.Draw(screenShadingTexture, screenShading, Color.White);
            }


            if (!menuActive && !inventoryActive)
                spriteBatch.Draw(menuTextures["crosshair"], crosshair, Color.White);
            //Draw buttons
            else if (menuActive)
            {
                back.Draw(spriteBatch);
                quit.Draw(spriteBatch);
            }
            //Draw creative inventory
            else if (inventoryActive)
            {
                MouseState currentMouseState = Mouse.GetState();

                spriteBatch.Draw(menuTextures["inventory"], inventory, Color.White);

                //Draw all blocks in the inventory
                for (int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < 9; j++)
                    {
                        if (items[i][j] != null)
                            spriteBatch.Draw(blockTextures[(int)items[i][j]],
                                new Rectangle(game.Window.ClientBounds.Width / 5 + 25 + j * 49, 78 + i * 50, 45, 45), Color.White);
                    }
                }

                //Draw active tools inside the inventory
                for (int i = 0; i < 9; i++)
                {
                    if (tools[i] != null)
                        spriteBatch.Draw(blockTextures[(int)tools[i]], 
                            new Rectangle(game.Window.ClientBounds.Width / 5 + 25 + i * 49, game.Window.ClientBounds.Height - 145, 45, 45), Color.White);
                }

                //Draw the selected dragged texture
                if (draggedTexture != null)
                {
                    spriteBatch.Draw(blockTextures[(int)draggedTexture], 
                        new Rectangle(currentMouseState.X, currentMouseState.Y, 45, 45), Color.White);
                }

                //Draw the name of the hovered texture
                if (hoveredTexture != null)
                {
                    Vector2 textSize = font14.MeasureString(blockNames[(ushort)hoveredTexture]);

                    spriteBatch.Draw(blackTexture,
                        new Rectangle(currentMouseState.X + 20, currentMouseState.Y, (int)textSize.X, (int)textSize.Y), Color.Black);
                    spriteBatch.DrawString(font14, blockNames[(ushort)hoveredTexture], 
                        new Vector2(currentMouseState.X + 20, currentMouseState.Y), Color.White);
                    hoveredTexture = null;
                }
            }

            spriteBatch.DrawString(font14, "FPS: " + fps.ToString(), new Vector2(10, 10), Color.White);

            spriteBatch.DrawString(font14, "Memory: " + GC.GetTotalMemory(false) / 1024, new Vector2(10, 30), Color.White);

            spriteBatch.End();

            graphics.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        void MenuControl(MouseState currentMouseState, Point mouseLoc)
        {
            if (back.Contains(mouseLoc))
            {
                back.Selected = true;

                if (Util.LeftButtonClicked(currentMouseState, previousMouseState))
                {
                    MenuSwitch();
                    Parameters.ExitedGameMenu = true;
                }
            }

            else if (quit.Contains(mouseLoc))
            {
                quit.Selected = true;

                if (Util.LeftButtonClicked(currentMouseState, previousMouseState))
                {
                    MenuSwitch();

                    Parameters.Inventory = tools;
                    Parameters.GameStarted = false;
                    Parameters.ExitedToMainMenu = true;
                }
            }
        }

        void InventoryControl(MouseState currentMouseState, Point mouseLoc)
        {
            bool isItemSelected = false;

            for (int i = 0; i < tools.Length; i++)
            {
                bool isClicked = Util.LeftButtonClicked(currentMouseState, previousMouseState) &&
                                 new Rectangle(game.Window.ClientBounds.Width / 5 + 23 + i * 49, game.Window.ClientBounds.Height - 145, 45, 45).Contains(mouseLoc);

                if (tools[i] is null && draggedTexture != null && isClicked)
                {
                    tools[i] = draggedTexture;
                    draggedTexture = null;
                }
                else if (draggedTexture is null && tools[i] != null && isClicked)
                {
                    draggedTexture = tools[i];
                    tools[i] = null;
                    isItemSelected = true;
                }
                else if (tools[i] != null && draggedTexture != null && isClicked)
                {
                    ushort? temp = draggedTexture;
                    draggedTexture = tools[i];
                    tools[i] = temp;
                    isItemSelected = true;
                }

                if (tools[i] != null &&
                    new Rectangle(game.Window.ClientBounds.Width / 5 + 23 + i * 49, game.Window.ClientBounds.Height - 145, 45, 45).Contains(mouseLoc))
                {
                    hoveredTexture = tools[i];
                }
            }

            if (!isItemSelected)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    for (int j = 0; j < items[i].Length; j++)
                    {
                        if (items[i][j] != null &&
                            Util.LeftButtonClicked(currentMouseState, previousMouseState) &&
                            new Rectangle(game.Window.ClientBounds.Width / 5 + 23 + j * 49, 76 + i * 50, 45, 45).Contains(mouseLoc))
                        {
                            draggedTexture = items[i][j];
                            isItemSelected = true;
                        }

                        if (items[i][j] != null &&
                            new Rectangle(game.Window.ClientBounds.Width / 5 + 23 + j * 49, 76 + i * 50, 45, 45).Contains(mouseLoc))
                        {
                            hoveredTexture = items[i][j];
                        }
                    }
                }
            }

            if (Util.RightButtonClicked(currentMouseState, previousMouseState) ||
                (!isItemSelected &&
                Util.LeftButtonClicked(currentMouseState, previousMouseState)))
            {
                draggedTexture = null;
            }
        }

        void InventorySwitch()
        {
            Vector2 screenCenter = new Vector2(game.Window.ClientBounds.Width / 2, game.Window.ClientBounds.Height / 2);
            Mouse.SetPosition((int)screenCenter.X, (int)screenCenter.Y);
            game.IsMouseVisible ^= true;

            inventoryActive ^= true;
            Parameters.GamePaused ^= true;
        }

        void MenuSwitch()
        {
            Vector2 screenCenter = new Vector2(game.Window.ClientBounds.Width / 2, game.Window.ClientBounds.Height / 2);
            Mouse.SetPosition((int)screenCenter.X, (int)screenCenter.Y);
            game.IsMouseVisible ^= true;

            menuActive ^= true;
            Parameters.GamePaused ^= true;
        }

        void ActiveToolSwitch(MouseState currentMouseState)
        {
            if (currentMouseState.ScrollWheelValue - previousScrollValue < 0)
            {
                if (activeToolIndex == 8)
                {
                    activeToolIndex = 0;
                }

                else
                {
                    activeToolIndex++;
                }
            }

            else if (currentMouseState.ScrollWheelValue - previousScrollValue > 0)
            {
                if (activeToolIndex == 0)
                {
                    activeToolIndex = 8;
                }

                else
                {
                    activeToolIndex--;
                }
            }

            selector.X = (game.Window.ClientBounds.Width / 2) - 225 + (activeToolIndex * 50);

            ActiveTool = tools[activeToolIndex];
        }
    }
}

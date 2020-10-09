using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System;


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
        Rectangle darkBackground;
        Texture2D darkBackgroundTexture;

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

        Dictionary<string, Texture2D> textures;
        Texture2D[] blockTextures;
        Dictionary<ushort, string> blockIndices;


        public GameMenu(MainGame _game, GraphicsDeviceManager _graphics,
                        Dictionary<string, Texture2D> _textures, Texture2D[] _blockTextures,
                        Dictionary<ushort, string> _blockIndices, SpriteFont[] fonts)
        {
            game = _game;

            graphics = _graphics;

            spriteBatch = new SpriteBatch(graphics.GraphicsDevice);

            textures = _textures;
            blockIndices = _blockIndices;

            blockTextures = _blockTextures;

            menuActive = false;
            inventoryActive = false;

            activeToolIndex = 0;

            tools = new ushort?[9];

            items = new ushort?[5][];

            for (int i = 0; i < items.Length; i++)
            {
                items[i] = new ushort?[9];
            }

            int row = 0, col = 0;
            foreach (var blockName in blockIndices)
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
                "button", "button_selector", "Back to game");
            quit = new Button((game.Window.ClientBounds.Width / 2) - 200, 4 * 70, 400, 70, 
                "button", "button_selector", "Quit the game");

            crosshair = new Rectangle((game.Window.ClientBounds.Width / 2) - 15, 
                (game.Window.ClientBounds.Height / 2) - 15, 30, 30);

            selector = new Rectangle((game.Window.ClientBounds.Width / 2) - 225, 
                game.Window.ClientBounds.Height - 50, 50, 50);

            inventory = new Rectangle((game.Window.ClientBounds.Width / 5), 30, 700, 700);

            toolbar = new Rectangle((game.Window.ClientBounds.Width / 2) - 225, 
                game.Window.ClientBounds.Height - 50, 450, 50);

            tool = new Rectangle((game.Window.ClientBounds.Width / 2) - 220, 
                game.Window.ClientBounds.Height - 45, 40, 40);

            darkBackground = new Rectangle(0, 0, game.Window.ClientBounds.Width, 
                game.Window.ClientBounds.Height);
            darkBackgroundTexture = new Texture2D(graphics.GraphicsDevice, 
                game.Window.ClientBounds.Width, game.Window.ClientBounds.Height);

            Color[] darkBackGroundColor = new Color[game.Window.ClientBounds.Width * game.Window.ClientBounds.Height];
            for (int i = 0; i < darkBackGroundColor.Length; i++) darkBackGroundColor[i] = new Color(Color.Black, 0.5f);

            darkBackgroundTexture.SetData(darkBackGroundColor);
        }

        public void Update()
        {
            MouseState currentMouseState = Mouse.GetState();
            KeyboardState currentKeyboardState = Keyboard.GetState();

            Parameters.ExitedMenu = false;

            if (!inventoryActive && Util.IsKeyPressed(Keys.Escape, currentKeyboardState, previousKeyboardState))
            {
                MenuSwitch();
            }

            if (!menuActive && Util.IsKeyPressed(Keys.E, currentKeyboardState, previousKeyboardState))
            {
                InventorySwitch();
            }

            else if (inventoryActive && (Util.IsKeyPressed(Keys.E, currentKeyboardState, previousKeyboardState) ||
                Util.IsKeyPressed(Keys.Escape, currentKeyboardState, previousKeyboardState)))
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

            spriteBatch.Draw(textures["toolbar"], toolbar, new Color(Color.DarkGray, 0.8f));

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
                spriteBatch.DrawString(font14, blockIndices[(ushort)tools[activeToolIndex]], new Vector2(toolbar.X, toolbar.Y - 20f), Color.White);
            }

            spriteBatch.Draw(textures["selector"], selector, Color.White);

            //Dim the background when menu is open
            if (Parameters.GamePaused)
            {
                spriteBatch.Draw(darkBackgroundTexture, darkBackground, Color.White);
            }


            if (!menuActive && !inventoryActive)
                spriteBatch.Draw(textures["crosshair"], crosshair, Color.White);
            //Draw in-game menu
            else if (menuActive)
            {
                spriteBatch.Draw(textures[back.Texture], back.Rect, Color.White);
                spriteBatch.DrawString(font24, back.Text, back.TextPosition, Color.White);
                if (back.IsSelected)
                    spriteBatch.Draw(textures[back.Selector], back.Rect, Color.White);

                back.IsSelected = false;

                spriteBatch.Draw(textures[quit.Texture], quit.Rect, Color.White);
                spriteBatch.DrawString(font24, quit.Text, quit.TextPosition, Color.White);
                if (quit.IsSelected)
                    spriteBatch.Draw(textures[quit.Selector], quit.Rect, Color.White);

                quit.IsSelected = false;
            }
            //Draw creative inventory
            else if (inventoryActive)
            {
                MouseState currentMouseState = Mouse.GetState();

                spriteBatch.Draw(textures["inventory"], inventory, Color.White);

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

                //Draw the name of the texture with a mouse pointer inside it
                if (hoveredTexture != null)
                {
                    spriteBatch.DrawString(font14, blockIndices[(ushort)hoveredTexture], 
                        new Vector2(currentMouseState.X + 20, currentMouseState.Y), Color.White);
                    hoveredTexture = null;
                }
            }

            spriteBatch.DrawString(font14, "FPS: " + fps.ToString(), new Vector2(10, 10), Color.White);

            spriteBatch.DrawString(font14, "Memory:" + GC.GetTotalMemory(false) / 1024, new Vector2(10, 30), Color.White);

            spriteBatch.End();

            graphics.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        void MenuControl(MouseState currentMouseState, Point mouseLoc)
        {
            if (back.Rect.Contains(mouseLoc))
            {
                back.IsSelected = true;

                if (Util.IsLeftButtonClicked(currentMouseState, previousMouseState))
                {
                    MenuSwitch();
                    Parameters.ExitedMenu = true;
                }
            }
            else if (quit.Rect.Contains(mouseLoc))
            {
                quit.IsSelected = true;

                if (Util.IsLeftButtonClicked(currentMouseState, previousMouseState))
                {
                    game.Exit();
                }
            }
        }

        void InventoryControl(MouseState currentMouseState, Point mouseLoc)
        {
            bool isItemSelected = false;

            for (int i = 0; i < tools.Length; i++)
            {
                bool isClicked = Util.IsLeftButtonClicked(currentMouseState, previousMouseState) &&
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
                            Util.IsLeftButtonClicked(currentMouseState, previousMouseState) &&
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

            if (Util.IsRightButtonClicked(currentMouseState, previousMouseState) ||
                (!isItemSelected &&
                Util.IsLeftButtonClicked(currentMouseState, previousMouseState)))
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

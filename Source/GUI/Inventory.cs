﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Utility;


namespace SharpCraft.GUI
{
    class Inventory
    {
        public ushort? SelectedItem { get; private set; }

        MainGame game;
        SpriteBatch spriteBatch;

        Action action;
        
        SpriteFont font;
        ReadOnlyDictionary<string, Texture2D> menuTextures;
        IList<Texture2D> blockTextures;
        ReadOnlyDictionary<ushort, string> blockNames;

        Label inventory, hotbar;
        Scroller scroller;

        Texture2D blackTexture;

        int previousScrollValue;
        int activeItemIndex;

        int screenWidth;
        int screenHeight;

        ushort?[,] items;

        ushort?[] hotbarItems;
        ushort? draggedTexture;
        ushort? hoveredTexture;
        Rectangle selector, selectedItem;


        public Inventory(MainGame game, SpriteBatch spriteBatch, SpriteFont font,
            Parameters parameters, Action a = null)
        {
            this.game = game;
            this.spriteBatch = spriteBatch;
            this.font = font;
            action = a;

            menuTextures = Assets.MenuTextures;
            blockNames = Assets.BlockNames;
            blockTextures = Assets.BlockTextures;

            screenWidth = game.Window.ClientBounds.Width;
            screenHeight = game.Window.ClientBounds.Height;

            activeItemIndex = 0;

            hotbarItems = new ushort?[9];
            if (parameters.Inventory.Any(item => item != null))
            {
                hotbarItems = parameters.Inventory;
            }

            int nRows = blockTextures.Count / 9 + 1;
            items = new ushort?[nRows, 9];

            int col = 0, row = 0;
            foreach (var blockName in blockNames)
            {
                items[row, col] = blockName.Key;
                col++;

                if (col % 9 == 0)
                {
                    col = 0;
                    row++;
                }
            }

            draggedTexture = null;
            hoveredTexture = null;

            SelectedItem = hotbarItems[activeItemIndex];

            previousScrollValue = 0;

            inventory = new Label(spriteBatch, menuTextures["inventory"],
                new Rectangle((screenWidth / 5), 30, 700, 700), Color.White);

            hotbar = new Label(spriteBatch, menuTextures["hotbar"], 
                new Rectangle((screenWidth / 2) - 225, screenHeight - 50, 450, 50), new Color(Color.DarkGray, 0.8f));

            Texture2D scrollerTexture = new Texture2D(game.GraphicsDevice, 10, 10);
            Color[] scrollerTextureColor = new Color[100];
            for (int i = 0; i < scrollerTextureColor.Length; i++)
            {
                scrollerTextureColor[i] = Color.LightGray;
            }
            scrollerTexture.SetData(scrollerTextureColor);

            int scrollStep = 300 / (nRows - 2) > 0 ? 300 / (nRows - 2) : 1;
            scroller = new Scroller(spriteBatch, scrollerTexture, nRows, scrollStep,
                new Rectangle((screenWidth - 163), 78, 35, 300 / (nRows / 5 + 1)));

            selector = new Rectangle((screenWidth / 2) - 225, screenHeight - 50, 50, 50);

            selectedItem = new Rectangle((screenWidth / 2) - 220, screenHeight - 45, 40, 40);

            blackTexture = new Texture2D(game.GraphicsDevice, 20, 10);
            Color[] blackTextureColor = new Color[200];
            for (int i = 0; i < blackTextureColor.Length; i++)
            {
                blackTextureColor[i] = Color.Black;
            }
            blackTexture.SetData(blackTextureColor);
        }

        public void SaveParameters(Parameters parameters)
        {
            parameters.Inventory = hotbarItems;
        }

        public void Draw(MouseState currentMouseState)
        {
            inventory.Draw();
            scroller.Draw();

            int startRow = scroller.Start;
            int endRow = scroller.End;

            //Draw all blocks in the inventory
            for (int row = startRow, i = 0; row < endRow; row++, i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    if (items[row, j] != null)
                    {
                        spriteBatch.Draw(blockTextures[(int)items[row, j]],
                            new Rectangle(screenWidth / 5 + 25 + j * 49, 78 + i * 50, 45, 45), Color.White);
                    }
                }
            }

            //Draw hotbar inside the inventory
            for (int i = 0; i < 9; i++)
            {
                if (hotbarItems[i] != null)
                {
                    spriteBatch.Draw(blockTextures[(int)hotbarItems[i]],
                        new Rectangle(screenWidth / 5 + 25 + i * 49,
                        screenHeight - 145, 45, 45), Color.White);
                }
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
                Vector2 textSize = font.MeasureString(blockNames[(ushort)hoveredTexture]);

                spriteBatch.Draw(blackTexture,
                    new Rectangle(currentMouseState.X + 20, currentMouseState.Y,
                    (int)textSize.X, (int)textSize.Y), Color.Black);

                spriteBatch.DrawString(font, blockNames[(ushort)hoveredTexture],
                    new Vector2(currentMouseState.X + 20, currentMouseState.Y), Color.White);

                hoveredTexture = null;
            }
        }

        public void DrawHotbar()
        {
            hotbar.Draw();

            for (int i = 0; i < hotbarItems.Length; i++)
            {
                selectedItem.X = (game.Window.ClientBounds.Width / 2) - 220 + (i * 50);

                if (hotbarItems[i] != null)
                {
                    spriteBatch.Draw(blockTextures[(ushort)hotbarItems[i]], selectedItem, Color.White);
                }
            }

            //Draw selected tool name
            if (hotbarItems[activeItemIndex] != null)
            {
                int x = (screenWidth / 2) - 225;
                int y = screenHeight - 50;
                Vector2 textSize = font.MeasureString(blockNames[(ushort)hotbarItems[activeItemIndex]]);

                spriteBatch.Draw(blackTexture,
                    new Rectangle(x, y - 20, (int)textSize.X, (int)textSize.Y), Color.Black);

                spriteBatch.DrawString(font, blockNames[(ushort)hotbarItems[activeItemIndex]],
                    new Vector2(x, y - 20), Color.White);
            }

            spriteBatch.Draw(menuTextures["selector"], selector, Color.White);
        }

        public void Update(KeyboardState currentKeyboardState, KeyboardState previousKeyboardState,
                           MouseState currentMouseState, MouseState previousMouseState, Point mouseLoc)
        {
            if (Util.KeyPressed(Keys.Escape, currentKeyboardState, previousKeyboardState) ||
                Util.KeyPressed(Keys.E, currentKeyboardState, previousKeyboardState))
            {
                action();
                return;
            }

            bool itemSelected = false;
            bool leftClick = Util.LeftButtonClicked(currentMouseState, previousMouseState);
            bool rightClick = Util.RightButtonClicked(currentMouseState, previousMouseState);

            scroller.Update(currentMouseState, previousMouseState, mouseLoc);

            int startRow = scroller.Start;
            int endRow = scroller.End;

            for (int i = 0; i < hotbarItems.Length; i++)
            {
                bool clicked = leftClick && new Rectangle(screenWidth / 5 + 23 + i * 49,
                               screenHeight - 145, 45, 45).Contains(mouseLoc);

                if (hotbarItems[i] is null && draggedTexture != null && clicked)
                {
                    hotbarItems[i] = draggedTexture;
                    draggedTexture = null;
                }
                else if (draggedTexture is null && hotbarItems[i] != null && clicked)
                {
                    draggedTexture = hotbarItems[i];
                    hotbarItems[i] = null;
                    itemSelected = true;
                }
                else if (hotbarItems[i] != null && draggedTexture != null && clicked)
                {
                    ushort? temp = draggedTexture;
                    draggedTexture = hotbarItems[i];
                    hotbarItems[i] = temp;
                    itemSelected = true;
                }

                if (hotbarItems[i] != null &&
                    new Rectangle(screenWidth / 5 + 23 + i * 49,
                    screenHeight - 145, 45, 45).Contains(mouseLoc))
                {
                    hoveredTexture = hotbarItems[i];
                }
            }

            if (!itemSelected)
            {
                for (int row = startRow, i = 0; row < endRow; row++, i++)
                {
                    for (int j = 0; j < 9; j++)
                    {
                        if (items[row, j] != null &&
                            leftClick &&
                            new Rectangle(screenWidth / 5 + 23 + j * 49, 76 + i * 50, 45, 45).Contains(mouseLoc))
                        {
                            draggedTexture = items[row, j];
                            itemSelected = true;
                        }

                        if (items[row, j] != null &&
                            new Rectangle(screenWidth / 5 + 23 + j * 49, 76 + i * 50, 45, 45).Contains(mouseLoc))
                        {
                            hoveredTexture = items[row, j];
                        }
                    }
                }
            }

            if (rightClick || (!itemSelected && leftClick))
            {
                draggedTexture = null;
            }
        }

        public void UpdateHotbar(MouseState currentMouseState)
        {
            if (currentMouseState.ScrollWheelValue - previousScrollValue < 0)
            {
                if (activeItemIndex == 8)
                {
                    activeItemIndex = 0;
                }

                else
                {
                    activeItemIndex++;
                }
            }

            else if (currentMouseState.ScrollWheelValue - previousScrollValue > 0)
            {
                if (activeItemIndex == 0)
                {
                    activeItemIndex = 8;
                }

                else
                {
                    activeItemIndex--;
                }
            }

            selector.X = (screenWidth / 2) - 225 + (activeItemIndex * 50);

            SelectedItem = hotbarItems[activeItemIndex];

            previousScrollValue = currentMouseState.ScrollWheelValue;
        }
    }
}

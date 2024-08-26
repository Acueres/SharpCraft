using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpCraft.Assets;
using SharpCraft.Utility;


namespace SharpCraft.GUI
{
    class SaveGrid
    {
        public Save SelectedSave
        {
            get
            {
                return saves[index];
            }
        }

        SpriteBatch spriteBatch;

        List<Save> saves;

        SaveSlot saveSlot;
        Label pageLabel;
        Button nextPage, previousPage;

        Rectangle shading;
        Texture2D shadingTexture;

        int index;
        int page;


        public SaveGrid(GraphicsDevice graphics, SpriteBatch spriteBatch, AssetServer assetServer, int screenWidth, int elementWidth,
            int elementHeight, List<Save> saves)
        {
            this.spriteBatch = spriteBatch;
            this.saves = saves;

            SpriteFont font = assetServer.GetFont(0);
            saveSlot = new SaveSlot(spriteBatch, (screenWidth / 2) - 300, 600, 2 * elementHeight,
                assetServer.GetMenuTexture("button_selector"), font);

            pageLabel = new Label(spriteBatch, "Page ",
                font, new Vector2(370, 350), Color.White);

            nextPage = new Button(graphics, spriteBatch, "Next", font,
                400, 380, elementWidth / 2, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    page++;
                    index = 3 * page;
                });

            previousPage = new Button(graphics, spriteBatch, "Previous", font,
                400 - elementWidth / 2, 380, elementWidth / 2, elementHeight,
                assetServer.GetMenuTexture("button"), assetServer.GetMenuTexture("button_selector"), () =>
                {
                    page--;
                    index = 3 * page;
                });

            shading = new Rectangle(0, 90, screenWidth, 260);
            shadingTexture = new Texture2D(graphics, screenWidth, 260);

            Color[] darkBackGroundColor = new Color[screenWidth * 260];
            for (int i = 0; i < darkBackGroundColor.Length; i++)
                darkBackGroundColor[i] = new Color(Color.Black, 0.7f);

            shadingTexture.SetData(darkBackGroundColor);

            index = 0;
            page = 0;
        }

        public void Draw()
        {
            spriteBatch.Draw(shadingTexture, shading, Color.White);

            for (int i = 3 * page, j = 0; i < 3 * (page + 1); i++, j++)
            {
                if (i >= saves.Count || saves.Count == 0)
                {
                    break;
                }

                saveSlot.DrawAt(100 + j * 80, saves[i], i == index);
            }

            pageLabel.Draw((page + 1).ToString());

            if (page >= saves.Count / 3f - 1)
            {
                nextPage.Inactive = true;
            }
            nextPage.Draw();
 
            if (page == 0)
            {
                previousPage.Inactive = true;
            }
            previousPage.Draw();
        }

        public void Update(MouseState currentMouseState, MouseState previousMouseState, Point mouseLoc)
        {
            bool leftClick = Util.LeftButtonClicked(currentMouseState, previousMouseState);

            if (index == saves.Count && index > 0)
            {
                index--;
            }

            page = index / 3;

            nextPage.Update(mouseLoc, leftClick);
            previousPage.Update(mouseLoc, leftClick);

            nextPage.Inactive = false;
            previousPage.Inactive = false;

            for (int i = 3 * page, j = 0; i < 3 * (page + 1); i++, j++)
            {
                if (i >= saves.Count || saves.Count == 0)
                {
                    break;
                }

                if (saveSlot.ContainsAt(100 + j * 80, mouseLoc) && leftClick)
                {
                    index = i;
                    break;
                }
            }
        }
    }
}

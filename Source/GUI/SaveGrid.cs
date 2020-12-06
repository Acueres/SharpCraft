using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;


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


        public SaveGrid(GraphicsDeviceManager graphics, SpriteBatch _spriteBatch, int screenWidth,
            List<Save> _saves, Dictionary<string, GUIElement> layout)
        {
            spriteBatch = _spriteBatch;
            saves = _saves;

            saveSlot = (SaveSlot)layout["Save Slot"];
            pageLabel = (Label)layout["Page"];

            nextPage = (Button)layout["Next Page"];
            nextPage.SetAction(() =>
            {
                page++;
                index = 3 * page;
            });

            previousPage = (Button)layout["Previous Page"];
            previousPage.SetAction(() =>
            {
                page--;
                index = 3 * page;
            });

            shading = new Rectangle(0, 90, screenWidth, 260);
            shadingTexture = new Texture2D(graphics.GraphicsDevice, screenWidth, 260);

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

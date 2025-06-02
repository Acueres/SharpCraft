using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SharpCraft.Rendering;

public class TextureAtlas
{
    public Texture2D Texture { get; init; }

    readonly Vector2 uvSize;
    readonly Dictionary<ushort, Vector2> textureUvTransforms = [];

    const int padding = 1;

    public TextureAtlas(int textureSize, int blockCount, GraphicsDevice graphics,
        Func<ushort, Texture2D> GetBlockTexture)
    {
        int cellSize = textureSize + padding;

        int texturesPerRow = (int)Math.Ceiling(Math.Sqrt(blockCount));
        int texturesPerColumn = (int)Math.Ceiling((double)blockCount / texturesPerRow);

        int atlasPixelWidth = texturesPerRow * cellSize;
        int atlasPixelHeight = texturesPerColumn * cellSize;

        uvSize = new Vector2(
                (float)textureSize / atlasPixelWidth,
                (float)textureSize / atlasPixelHeight
            );

        Texture = new Texture2D(graphics, atlasPixelWidth, atlasPixelHeight);
        Color[] atlasData = new Color[atlasPixelWidth * atlasPixelHeight];

        for (int i = 0; i < blockCount; i++)
        {
            Color[] textureColor = new Color[textureSize * textureSize];
            GetBlockTexture((ushort)i).GetData(textureColor);

            int column = i % texturesPerRow;
            int row = i / texturesPerRow;

            // Calculate the top-left pixel position for this texture in the atlas
            int destX = column * cellSize;
            int destY = row * cellSize;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    int atlasIndex = (destY + y) * atlasPixelWidth + (destX + x);
                    int textureIndex = y * textureSize + x;
                    if (atlasIndex < atlasData.Length)
                    {
                        atlasData[atlasIndex] = textureColor[textureIndex];
                    }
                }
            }

            // Cache the UV transformation start
            Vector2 uvStart = new(
                (float)destX / atlasPixelWidth,
                (float)destY / atlasPixelHeight
            );
            textureUvTransforms[(ushort)i] = uvStart;
        }
        Texture.SetData(atlasData);
    }

    public Vector2 GetUV(ushort index, Vector2 uvBase)
    {
        var uvStart = textureUvTransforms[index];
        return uvStart + uvBase * uvSize;
    }
}

using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

using SharpCraft.Assets;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.Persistence;
using SharpCraft.Rendering.Meshers;

namespace SharpCraft.Rendering;

class Renderer
{
    readonly Texture2D atlas;

    readonly RegionRenderer regionRenderer;

    public Renderer(GraphicsDevice graphics, BlockSelector blockSelector,
        AssetServer assetServer, BlockMetadataProvider blockMetadata, ScreenshotTaker screenshotTaker,
        ChunkMesher chunkMesher)
    { 

        var effect = assetServer.GetEffect;
        blockSelector.SetEffect(effect);

        atlas = new Texture2D(graphics, 64, blockMetadata.BlockCount * 64);
        Color[] atlasData = new Color[atlas.Width * atlas.Height];

        for (int i = 0; i < blockMetadata.BlockCount; i++)
        {
            Color[] textureColor = new Color[64 * 64];
            assetServer.GetBlockTexture((ushort)i).GetData(textureColor);

            for (int j = 0; j < textureColor.Length; j++)
            {
                atlasData[(i * textureColor.Length) + j] = textureColor[j];
            }
        }
        atlas.SetData(atlasData);

        regionRenderer = new RegionRenderer(graphics, effect, chunkMesher, screenshotTaker, blockSelector, atlas);
    }

    public void Render(IEnumerable<IChunk> chunks, Camera camera, Time time)
    {
        regionRenderer.Render(chunks, camera, time.LightIntensity);
    }
}
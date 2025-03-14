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
    readonly UIRenderer uiRenderer;

    public Renderer(GraphicsDevice graphics, AssetServer assetServer,
        BlockMetadataProvider blockMetadata, ScreenshotTaker screenshotTaker,
        ChunkMesher chunkMesher, BlockOutlineMesher blockOutlineMesher)
    { 

        var effect = assetServer.GetEffect;

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

        regionRenderer = new RegionRenderer(graphics, effect, chunkMesher, screenshotTaker, atlas);
        uiRenderer = new UIRenderer(graphics, effect, blockOutlineMesher, assetServer);
    }

    public void Render(IEnumerable<IChunk> chunks, Camera camera, Time time)
    {
        regionRenderer.Render(chunks, camera, time.LightIntensity);
        uiRenderer.Render();
    }
}
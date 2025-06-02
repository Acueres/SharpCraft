using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Assets;
using SharpCraft.Utilities;
using SharpCraft.World.Chunks;
using SharpCraft.Persistence;
using SharpCraft.Rendering.Meshers;

namespace SharpCraft.Rendering;

class Renderer
{
    readonly RegionRenderer regionRenderer;
    readonly UIRenderer uiRenderer;

    public Renderer(Region region, GraphicsDevice graphics, AssetServer assetServer,
        ScreenshotTaker screenshotTaker,
        ChunkMesher chunkMesher, BlockOutlineMesher blockOutlineMesher)
    { 
        var effect = assetServer.Effect;
        
        regionRenderer = new RegionRenderer(region, graphics, effect, chunkMesher, screenshotTaker, assetServer.Atlas);
        uiRenderer = new UIRenderer(graphics, effect, blockOutlineMesher, assetServer);
    }

    public void Render(Camera camera, Time time)
    {
        regionRenderer.Render(camera, time.LightIntensity);
        uiRenderer.Render();
    }
}
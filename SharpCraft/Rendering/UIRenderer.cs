using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Assets;
using SharpCraft.Rendering.Meshers;
using SharpCraft.Utilities;

namespace SharpCraft.Rendering;

class UIRenderer(GraphicsDevice graphics, Effect effect, BlockOutlineMesher blockOutlineMesher, AssetServer assetServer)
{
    readonly GraphicsDevice graphics = graphics;
    readonly Effect effect = effect;
    readonly BlockOutlineMesher blockOutlineMesher = blockOutlineMesher;
    readonly Texture2D blockOutline = assetServer.GetMenuTexture("block_outline");
    readonly DynamicVertexBuffer blockOutlineBuffer = new(graphics, typeof(VertexPositionTextureLight),
                        36, BufferUsage.WriteOnly);

    public void Render()
    {
        // Render selected block outline
        if (blockOutlineMesher.Vertices.Length == 0) return;

        effect.Parameters["Alpha"].SetValue(1f);
        effect.Parameters["Texture"].SetValue(blockOutline);

        blockOutlineBuffer.SetData(blockOutlineMesher.Vertices);
        graphics.SetVertexBuffer(blockOutlineBuffer);

        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, blockOutlineMesher.Vertices.Length / 3);
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

using SharpCraft.Persistence;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.Rendering.Meshers;

namespace SharpCraft.Rendering;

class RegionRenderer
{
    readonly GraphicsDevice graphics;
    readonly ScreenshotTaker screenshotTaker;

    readonly Texture2D atlas;
    readonly Effect effect;
    readonly DynamicVertexBuffer buffer;
    readonly ChunkMesher chunkMesher;

    public RegionRenderer(GraphicsDevice graphics, Effect effect, ChunkMesher chunkMesher,
        ScreenshotTaker screenshotTaker, Texture2D atlas)
    {
        this.graphics = graphics;
        this.effect = effect;
        this.chunkMesher = chunkMesher;
        this.screenshotTaker = screenshotTaker;
        this.atlas = atlas;

        buffer = new DynamicVertexBuffer(graphics, typeof(VertexPositionTextureLight),
                    (int)2e4, BufferUsage.WriteOnly);
    }

    public void Render(IEnumerable<Chunk> chunks, Camera camera, float lightIntensity)
    {
        Vector3 chunkMax = new(Chunk.Size);

        HashSet<Chunk> visibleChunks = [];

        effect.Parameters["World"].SetValue(Matrix.Identity);
        effect.Parameters["View"].SetValue(camera.View);
        effect.Parameters["Projection"].SetValue(camera.Projection);
        effect.Parameters["Texture"].SetValue(atlas);
        effect.Parameters["LightIntensity"].SetValue(lightIntensity);

        graphics.Clear(Color.Lerp(Color.SkyBlue, Color.Black, 1f - lightIntensity));

        //Drawing opaque blocks
        foreach (Chunk chunk in chunks)
        {
            if (chunk.IsEmpty || !chunk.IsReady) continue;

            BoundingBox chunkBounds = new(chunk.Position, chunkMax + chunk.Position);

            bool isChunkVisible = false;
            if (camera.Frustum.Intersects(chunkBounds))
            {
                visibleChunks.Add(chunk);
                isChunkVisible = true;
            }

            if (isChunkVisible && chunk.ActiveBlocksCount > 0)
            {
                effect.Parameters["Alpha"].SetValue(1f);

                var vertices = chunkMesher.GetVertices(chunk.Index);

                if (vertices.Length == 0) continue;

                buffer.SetData(vertices);
                graphics.SetVertexBuffer(buffer);

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, vertices.Length / 3);
                }
            }
        }

        //Drawing transparent blocks
        foreach (Chunk chunk in visibleChunks)
        {
            var vertices = chunkMesher.GetTransparentVertices(chunk.Index);
            if (vertices.Length == 0) continue;

            effect.Parameters["Alpha"].SetValue(0.7f);

            buffer.SetData(vertices);
            graphics.SetVertexBuffer(buffer);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, vertices.Length / 3);
            }

        }

        if (screenshotTaker.TakeScreenshot)
        {
            screenshotTaker.Screenshot(DateTime.Now.ToString());
        }
    }
}
using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpCraft.Handlers;
using SharpCraft.World;
using SharpCraft.Utility;
using SharpCraft.Assets;


namespace SharpCraft.Rendering
{
    class Renderer
    {
        MainGame game;
        GraphicsDevice graphics;
        Effect effect;
        Region region;
        ScreenshotHandler screenshotHandler;
        BlockSelector blockSelector;

        Time time;

        Texture2D atlas;

        Chunk currentChunk;

        DynamicVertexBuffer buffer;

        public Renderer(MainGame game, GraphicsDevice graphics, Time time,
            Region region, ScreenshotHandler screenshotTaker, BlockSelector blockSelector,
            AssetServer assetServer, BlockMetadataProvider blockMetadata)
        {
            this.game = game;
            this.graphics = graphics;
            this.region = region;
            screenshotHandler = screenshotTaker;
            this.blockSelector = blockSelector;

            effect = assetServer.GetEffect;

            blockSelector.SetEffect(effect);

            this.time = time;

            atlas = new Texture2D(graphics, 64, blockMetadata.GetBlocksCount * 64);
            Color[] atlasData = new Color[atlas.Width * atlas.Height];

            for (int i = 0; i < blockMetadata.GetBlocksCount; i++)
            {
                Color[] textureColor = new Color[64 * 64];
                assetServer.GetBlockTexture((ushort)i).GetData(textureColor);

                for (int j = 0; j < textureColor.Length; j++)
                {
                    atlasData[(i * textureColor.Length) + j] = textureColor[j];
                }
            }
            atlas.SetData(atlasData);

            buffer = new DynamicVertexBuffer(graphics, typeof(VertexPositionTextureLight),
                        (int)2e4, BufferUsage.WriteOnly);
        }

        public void Draw(Player player)
        {
            Vector3 chunkMax = new(Chunk.Size, 128, Chunk.Size);

            float lightIntensity = time.LightIntensity;

            HashSet<Vector3I> visibleChunkIndexes = [];

            effect.Parameters["World"].SetValue(Matrix.Identity);
            effect.Parameters["View"].SetValue(player.Camera.View);
            effect.Parameters["Projection"].SetValue(player.Camera.Projection);
            effect.Parameters["Texture"].SetValue(atlas);
            effect.Parameters["LightIntensity"].SetValue(lightIntensity);

            if (!game.Paused)
            {
                time.Update();
            }

            graphics.Clear(Color.Lerp(Color.SkyBlue, Color.Black, 1f - lightIntensity));

            //Drawing opaque blocks
            foreach (Vector3I index in region.GetActiveChunkIndexes())
            {
                currentChunk = region.GetChunk(index);

                BoundingBox chunkBounds = new(-currentChunk.Position, chunkMax - currentChunk.Position);

                bool isChunkVisible = false;
                if (player.Camera.Frustum.Contains(chunkBounds) != ContainmentType.Disjoint)
                {
                    visibleChunkIndexes.Add(index);
                    isChunkVisible = true;
                }

                if (isChunkVisible && currentChunk.VertexCount > 0)
                {
                    effect.Parameters["Alpha"].SetValue(1f);

                    buffer.SetData(currentChunk.GetVertices());
                    graphics.SetVertexBuffer(buffer);

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, currentChunk.VertexCount / 3);
                    }
                }
            }

            //Drawing transparent blocks
            foreach (Vector3I index in visibleChunkIndexes)
            {
                currentChunk = region.GetChunk(index);

                if (currentChunk.TransparentVertexCount > 0)
                {
                    effect.Parameters["Alpha"].SetValue(0.7f);

                    buffer.SetData(currentChunk.GetTransparentVertices());
                    graphics.SetVertexBuffer(buffer);

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, currentChunk.TransparentVertexCount / 3);
                    }
                }
            }

            if (screenshotHandler.TakeScreenshot)
            {
                screenshotHandler.Screenshot(DateTime.Now.ToString());
            }

            blockSelector.Draw();
        }
    }
}

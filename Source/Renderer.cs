using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft
{
    class Renderer
    {
        GraphicsDeviceManager graphics;
        Effect effect;
        Dictionary<Vector3, Chunk> region;

        Time time;

        Texture2D[] blockTextures;
        Texture2D atlas;

        int size;

        Chunk currentChunk;

        DynamicVertexBuffer buffer;


        public Renderer(GraphicsDeviceManager _graphics, Time _time,
            Dictionary<Vector3, Chunk> _region, Parameters parameters)
        {
            graphics = _graphics;
            effect = Assets.Effect;
            region = _region;

            time = _time;

            size = Settings.ChunkSize;

            blockTextures = Assets.BlockTextures;

            atlas = new Texture2D(graphics.GraphicsDevice, 64, blockTextures.Length * 64);
            Color[] atlasData = new Color[atlas.Width * atlas.Height];

            for (int i = 0; i < blockTextures.Length; i++)
            {
                Color[] textureColor = new Color[64 * 64];
                blockTextures[i].GetData(textureColor);

                for (int j = 0; j < textureColor.Length; j++)
                {
                    atlasData[(i * textureColor.Length) + j] = textureColor[j];
                }
            }
            atlas.SetData(atlasData);

            buffer = new DynamicVertexBuffer(graphics.GraphicsDevice, typeof(VertexPositionTextureLight),
                        (int)2e4, BufferUsage.WriteOnly);
        }

        public void Draw(Vector3[] activeChunks, Player player)
        {
            Vector3 chunkMax = new Vector3(size, 128, size);
            Vector3 position;

            float lightIntensity = time.LightIntensity;

            bool[] chunkVisible = new bool[activeChunks.Length];

            effect.Parameters["World"].SetValue(Matrix.Identity);
            effect.Parameters["View"].SetValue(player.Camera.View);
            effect.Parameters["Projection"].SetValue(player.Camera.Projection);
            effect.Parameters["Texture"].SetValue(atlas);
            effect.Parameters["LightIntensity"].SetValue(lightIntensity);

            if (!GameState.Paused)
            {
                time.Update();
            }

            graphics.GraphicsDevice.Clear(Color.Lerp(Color.SkyBlue, Color.Black, 1f - lightIntensity));

            //Drawing opaque blocks
            for (int i = 0; i < activeChunks.Length; i++)
            {
                position = activeChunks[i];
                currentChunk = region[position];

                BoundingBox chunkBounds = new BoundingBox(-position, chunkMax - position);

                chunkVisible[i] = player.Camera.Frustum.Contains(chunkBounds) != ContainmentType.Disjoint;

                if (chunkVisible[i] && currentChunk.VertexCount > 0)
                {
                    effect.Parameters["Alpha"].SetValue(1f);

                    buffer.SetData(currentChunk.Vertices);
                    graphics.GraphicsDevice.SetVertexBuffer(buffer);

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        graphics.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, currentChunk.VertexCount / 3);
                    }
                }
            }

            //Drawing transparent blocks
            for (int i = 0; i < activeChunks.Length; i++)
            {
                currentChunk = region[activeChunks[i]];

                if (chunkVisible[i] && currentChunk.TransparentVertexCount > 0)
                {
                    effect.Parameters["Alpha"].SetValue(0.7f);

                    buffer.SetData(currentChunk.TransparentVertices);
                    graphics.GraphicsDevice.SetVertexBuffer(buffer);

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        graphics.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, currentChunk.TransparentVertexCount / 3);
                    }
                }
            }
        }
    }
}

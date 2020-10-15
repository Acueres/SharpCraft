using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;


namespace SharpCraft
{
    class Renderer
    {
        GraphicsDeviceManager graphics;
        BasicEffect effect;
        Dictionary<Vector3, Chunk> region;
        Texture2D[] blockTextures;
        Texture2D atlas;
        int size;
        Chunk currentChunk;

        DynamicVertexBuffer buffer;


        public Renderer(GraphicsDeviceManager _graphics, BasicEffect _effect,
            Dictionary<Vector3, Chunk> _region, Texture2D[] _blockTextures)
        {
            graphics = _graphics;
            effect = _effect;
            size = Parameters.ChunkSize;
            region = _region;
            blockTextures = _blockTextures;

            buffer = new DynamicVertexBuffer(graphics.GraphicsDevice, typeof(VertexPositionNormalTexture),
                        (int)2e4, BufferUsage.WriteOnly);

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
        }

        public void Draw(Vector3[] activeChunks, Player player)
        {
            Vector3 chunkMax = new Vector3(0.8f * size, 128, 0.8f * size);
            Vector3 position;

            bool[] chunkVisible = new bool[activeChunks.Length];

            effect.EnableDefaultLighting();

            effect.TextureEnabled = true;
            effect.Texture = atlas;

            effect.View = player.Camera.View;
            effect.Projection = player.Camera.Projection;

            effect.LightingEnabled = true;
            effect.DirectionalLight0.DiffuseColor = Color.SkyBlue.ToVector3(); // a red light
            effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(0.5f, 0.5f, 0));  // coming along the x-axis
            effect.DirectionalLight0.SpecularColor = Color.LightYellow.ToVector3(); // with green highlights
            effect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.2f);

            //Drawing opaque blocks
            for (int i = 0; i < activeChunks.Length; i++)
            {
                position = activeChunks[i];
                currentChunk = region[position];

                BoundingBox chunkBounds = new BoundingBox(-position, chunkMax - position);

                chunkVisible[i] = player.Camera.Frustum.Contains(chunkBounds) != ContainmentType.Disjoint;

                if (chunkVisible[i] && currentChunk.VertexCount > 0)
                {
                    effect.Alpha = 1f;

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
                    effect.Alpha = 0.7f;

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

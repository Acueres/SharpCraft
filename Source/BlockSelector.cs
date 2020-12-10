using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft
{
    class BlockSelector
    {
        GraphicsDeviceManager graphics;
        Effect effect;

        VertexPositionTextureLight[] vertices;
        List<VertexPositionTextureLight> vertexList;

        Cube cube;
        Texture2D texture;

        DynamicVertexBuffer buffer;


        public BlockSelector(GraphicsDeviceManager _graphics)
        {
            graphics = _graphics;

            vertexList = new List<VertexPositionTextureLight>(36);

            cube = new Cube();
            texture = Assets.MenuTextures["block_selector"];

            buffer = new DynamicVertexBuffer(graphics.GraphicsDevice, typeof(VertexPositionTextureLight),
                        36, BufferUsage.WriteOnly);
        }

        public void SetEffect(Effect _effect)
        {
            effect = _effect;
        }

        public void Draw()
        {
            if (vertices != null)
            {
                effect.Parameters["Alpha"].SetValue(1f);
                effect.Parameters["Texture"].SetValue(texture);

                buffer.SetData(vertices);
                graphics.GraphicsDevice.SetVertexBuffer(buffer);

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphics.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vertices.Length / 3);
                }
            }
        }

        public void Update(bool[] visibleFaces, Vector3 position)
        {
            for (int face = 0; face < 6; face++)
            {
                if (visibleFaces[face])
                {
                    for (int i = 0; i < 6; i++)
                    {
                        VertexPositionTextureLight vertex = cube.Faces[face][i];

                        vertex.Position += position;
                        vertex.Light = 16;

                        vertexList.Add(vertex);
                    }
                }
            }

            vertices = vertexList.ToArray();
            vertexList.Clear();
        }

        public void Clear()
        {
            vertices = null;
        }
    }
}

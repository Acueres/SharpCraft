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

        Dictionary<int, char> faceMap;

        Cube cube;
        Texture2D texture;

        DynamicVertexBuffer buffer;


        public BlockSelector(GraphicsDeviceManager _graphics)
        {
            graphics = _graphics;

            vertices = new VertexPositionTextureLight[36];

            faceMap = new Dictionary<int, char>
            {
                [0] = 'Z',
                [1] = 'Z',
                [2] = 'Y',
                [3] = 'Y',
                [4] = 'X',
                [5] = 'X'
            };

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

        public void Update(bool[] visibleFaces, Vector3 position, Vector3 direction)
        {
            Array.Clear(vertices, 0, 36);

            char maxComponent = Util.MaxVectorComponent(direction);

            for (int face = 0; face < 6; face++)
            {
                if (faceMap[face] == maxComponent && visibleFaces[face])
                {
                    for (int i = 0; i < 6; i++)
                    {
                        VertexPositionTextureLight vertex = cube.Faces[face][i];

                        vertex.Position += position;

                        vertices[i + face * 6] = vertex;
                    }
                }
            }
        }

        public void Clear()
        {
            Array.Clear(vertices, 0, 36);
        }
    }
}

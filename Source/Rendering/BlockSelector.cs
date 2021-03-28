using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Models;
using SharpCraft.Utility;


namespace SharpCraft.Rendering
{
    class BlockSelector
    {
        GraphicsDevice graphics;
        Effect effect;

        VertexPositionTextureLight[] vertices;

        Dictionary<int, char> faceMap;

        Cube cube;
        Texture2D texture;

        DynamicVertexBuffer buffer;


        public BlockSelector(GraphicsDevice graphics)
        {
            this.graphics = graphics;

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

            buffer = new DynamicVertexBuffer(graphics, typeof(VertexPositionTextureLight),
                        36, BufferUsage.WriteOnly);
        }

        public void SetEffect(Effect effect)
        {
            this.effect = effect;
        }

        public void Draw()
        {
            effect.Parameters["Alpha"].SetValue(1f);
            effect.Parameters["Texture"].SetValue(texture);

            buffer.SetData(vertices);
            graphics.SetVertexBuffer(buffer);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, vertices.Length / 3);
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

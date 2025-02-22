﻿using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpCraft.Assets;
using SharpCraft.Utility;


namespace SharpCraft.World.Blocks
{
    class BlockSelector
    {
        GraphicsDevice graphics;
        Effect effect;

        List<VertexPositionTextureLight> vertices = [];

        Dictionary<int, char> faceMap;

        Texture2D texture;

        DynamicVertexBuffer buffer;


        public BlockSelector(GraphicsDevice graphics, AssetServer assetServer)
        {
            this.graphics = graphics;

            faceMap = new Dictionary<int, char>
            {
                [0] = 'Z',
                [1] = 'Z',
                [2] = 'Y',
                [3] = 'Y',
                [4] = 'X',
                [5] = 'X'
            };

            texture = assetServer.GetMenuTexture("block_outline");

            buffer = new DynamicVertexBuffer(graphics, typeof(VertexPositionTextureLight),
                        36, BufferUsage.WriteOnly);
        }

        public void SetEffect(Effect effect)
        {
            this.effect = effect;
        }

        public void Draw()
        {
            if (vertices.Count == 0) return;

            effect.Parameters["Alpha"].SetValue(1f);
            effect.Parameters["Texture"].SetValue(texture);

            buffer.SetData(vertices.ToArray());
            graphics.SetVertexBuffer(buffer);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, vertices.Count / 3);
            }
        }

        public void Update(FacesState visibleFaces, Vector3 position, Vector3 direction)
        {
            Clear();

            char maxComponent = Util.MaxVectorComponent(direction);

            foreach (Faces face in visibleFaces.GetFaces())
            {
                if (faceMap[(byte)face] != maxComponent) continue;

                for (int i = 0; i < 6; i++)
                {
                    VertexPositionTextureLight vertex = Cube.Faces[(byte)face][i];

                    vertex.Position += position;
                    vertices.Add(vertex);
                }
            }
        }

        public void Clear()
        {
            vertices.Clear();
        }
    }
}

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Assets;
using SharpCraft.Utilities;


namespace SharpCraft.World.Blocks
{
    class BlockSelector
    {
        readonly GraphicsDevice graphics;
        Effect effect;

        List<VertexPositionTextureLight> vertices = [];

        readonly static Dictionary<Faces, AxisDirection> faceMap = new()
        {
            [Faces.ZPos] = AxisDirection.Z,
            [Faces.ZNeg] = AxisDirection.Z,
            [Faces.YPos] = AxisDirection.Y,
            [Faces.YNeg] = AxisDirection.Y,
            [Faces.XPos] = AxisDirection.X,
            [Faces.XNeg] = AxisDirection.X
        };

        readonly Texture2D texture;
        readonly DynamicVertexBuffer buffer;


        public BlockSelector(GraphicsDevice graphics, AssetServer assetServer)
        {
            this.graphics = graphics;

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

            AxisDirection dominantAxis = Util.GetDominantAxis(direction);

            foreach (Faces face in visibleFaces.GetFaces())
            {
                if (faceMap[face] != dominantAxis) continue;

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

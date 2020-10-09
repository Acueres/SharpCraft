using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace SharpCraft
{
    class Cube
    {
        public VertexPositionNormalTexture[][] Faces;

        public Cube(float side = 0.5f)
        {
            VertexPositionNormalTexture[] front = new VertexPositionNormalTexture[6];
            front[0].Position = new Vector3(-side, -side, side);
            front[1].Position = new Vector3(-side, side, side);
            front[2].Position = new Vector3(side, -side, side);
            front[3].Position = front[1].Position;
            front[4].Position = new Vector3(side, side, side);
            front[5].Position = front[2].Position;

            VertexPositionNormalTexture[] back = new VertexPositionNormalTexture[6];
            back[0].Position = new Vector3(side, -side, -side);
            back[1].Position = new Vector3(side, side, -side);
            back[2].Position = new Vector3(-side, -side, -side);
            back[3].Position = back[1].Position;
            back[4].Position = new Vector3(-side, side, -side);
            back[5].Position = back[2].Position;

            VertexPositionNormalTexture[] top = new VertexPositionNormalTexture[6];
            top[0].Position = new Vector3(-side, side, side);
            top[1].Position = new Vector3(-side, side, -side);
            top[2].Position = new Vector3(side, side, side);
            top[3].Position = top[1].Position;
            top[4].Position = new Vector3(side, side, -side);
            top[5].Position = top[2].Position;

            VertexPositionNormalTexture[] bottom = new VertexPositionNormalTexture[6];
            bottom[0].Position = new Vector3(-side, -side, -side);
            bottom[1].Position = new Vector3(-side, -side, side);
            bottom[2].Position = new Vector3(side, -side, -side);
            bottom[3].Position = bottom[1].Position;
            bottom[4].Position = new Vector3(side, -side, side);
            bottom[5].Position = bottom[2].Position;

            VertexPositionNormalTexture[] right = new VertexPositionNormalTexture[6];
            right[0].Position = new Vector3(side, -side, side);
            right[1].Position = new Vector3(side, side, side);
            right[2].Position = new Vector3(side, -side, -side);
            right[3].Position = right[1].Position;
            right[4].Position = new Vector3(side, side, -side);
            right[5].Position = right[2].Position;

            VertexPositionNormalTexture[] left = new VertexPositionNormalTexture[6];
            left[0].Position = new Vector3(-side, -side, -side);
            left[1].Position = new Vector3(-side, side, -side);
            left[2].Position = new Vector3(-side, -side, side);
            left[3].Position = left[1].Position;
            left[4].Position = new Vector3(-side, side, side);
            left[5].Position = left[2].Position;

            Faces = new VertexPositionNormalTexture[][] { front, back, top, bottom, right, left };

            for (int i = 0; i < 6; i++)
            {
                Faces[i][0].TextureCoordinate = new Vector2(1, 1);
                Faces[i][1].TextureCoordinate = new Vector2(1, 0);
                Faces[i][2].TextureCoordinate = new Vector2(0, 1);
                Faces[i][3].TextureCoordinate = Faces[i][1].TextureCoordinate;
                Faces[i][4].TextureCoordinate = new Vector2(0, 0);
                Faces[i][5].TextureCoordinate = Faces[i][2].TextureCoordinate;
            }

            for (int i = 0; i < 6; i++)
            {
                Vector3 normal = FaceNormal(Faces[i]);

                for (int j = 0; j < 6; j++)
                {
                    Faces[i][j].Normal = normal;
                }
            }
        }

        Vector3 FaceNormal(VertexPositionNormalTexture[] vertices)
        {
            return Vector3.Cross(vertices[1].Position - vertices[0].Position, vertices[2].Position - vertices[0].Position);
        }
    }
}

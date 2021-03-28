using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using SharpCraft.Rendering;


namespace SharpCraft.Models
{
    class Cube
    {
        public VertexPositionTextureLight[][] Faces;

        public Cube(float size = 0.5f)
        {
            VertexPositionTextureLight[] front = new VertexPositionTextureLight[6];
            front[0].Position = new Vector3(-size, -size, size);
            front[1].Position = new Vector3(-size, size, size);
            front[2].Position = new Vector3(size, -size, size);
            front[3].Position = front[1].Position;
            front[4].Position = new Vector3(size, size, size);
            front[5].Position = front[2].Position;

            VertexPositionTextureLight[] back = new VertexPositionTextureLight[6];
            back[0].Position = new Vector3(size, -size, -size);
            back[1].Position = new Vector3(size, size, -size);
            back[2].Position = new Vector3(-size, -size, -size);
            back[3].Position = back[1].Position;
            back[4].Position = new Vector3(-size, size, -size);
            back[5].Position = back[2].Position;

            VertexPositionTextureLight[] top = new VertexPositionTextureLight[6];
            top[0].Position = new Vector3(-size, size, size);
            top[1].Position = new Vector3(-size, size, -size);
            top[2].Position = new Vector3(size, size, size);
            top[3].Position = top[1].Position;
            top[4].Position = new Vector3(size, size, -size);
            top[5].Position = top[2].Position;

            VertexPositionTextureLight[] bottom = new VertexPositionTextureLight[6];
            bottom[0].Position = new Vector3(-size, -size, -size);
            bottom[1].Position = new Vector3(-size, -size, size);
            bottom[2].Position = new Vector3(size, -size, -size);
            bottom[3].Position = bottom[1].Position;
            bottom[4].Position = new Vector3(size, -size, size);
            bottom[5].Position = bottom[2].Position;

            VertexPositionTextureLight[] right = new VertexPositionTextureLight[6];
            right[0].Position = new Vector3(size, -size, size);
            right[1].Position = new Vector3(size, size, size);
            right[2].Position = new Vector3(size, -size, -size);
            right[3].Position = right[1].Position;
            right[4].Position = new Vector3(size, size, -size);
            right[5].Position = right[2].Position;

            VertexPositionTextureLight[] left = new VertexPositionTextureLight[6];
            left[0].Position = new Vector3(-size, -size, -size);
            left[1].Position = new Vector3(-size, size, -size);
            left[2].Position = new Vector3(-size, -size, size);
            left[3].Position = left[1].Position;
            left[4].Position = new Vector3(-size, size, size);
            left[5].Position = left[2].Position;

            Faces = new VertexPositionTextureLight[][] { front, back, top, bottom, right, left };

            for (int i = 0; i < 6; i++)
            {
                Faces[i][0].TextureCoordinate = new Vector2(1, 1);
                Faces[i][1].TextureCoordinate = new Vector2(1, 0);
                Faces[i][2].TextureCoordinate = new Vector2(0, 1);
                Faces[i][3].TextureCoordinate = Faces[i][1].TextureCoordinate;
                Faces[i][4].TextureCoordinate = new Vector2(0, 0);
                Faces[i][5].TextureCoordinate = Faces[i][2].TextureCoordinate;
            }
        }

        Vector3 FaceNormal(VertexPositionNormalTexture[] vertices)
        {
            return Vector3.Cross(vertices[1].Position - vertices[0].Position, vertices[2].Position - vertices[0].Position);
        }
    }
}

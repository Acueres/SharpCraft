using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

using SharpCraft.Models;
using SharpCraft.Rendering;

namespace SharpCraft.World
{
    public sealed partial class Chunk
    {
        public bool UpdateMesh { private get; set; }

        public VertexPositionTextureLight[] Vertices;
        public VertexPositionTextureLight[] TransparentVertices;

        public List<VertexPositionTextureLight> VertexList;
        public List<VertexPositionTextureLight> TransparentVertexList;

        public int VertexCount;
        public int TransparentVertexCount;

        public void CalculateMesh()
        {
            bool[] visibleFaces = new bool[6];
            byte[] lightValues = new byte[6];

            for (int i = 0; i < Active.Count; i++)
            {
                int y = Active[i].Y;
                int x = Active[i].X;
                int z = Active[i].Z;

                Vector3 blockPosition = new Vector3(x, y, z) - Position;

                visibleFaces = GetVisibleFaces(visibleFaces, y, x, z);
                lightValues = GetFacesLight(lightValues, visibleFaces, y, x, z);

                for (int face = 0; face < 6; face++)
                {
                    if (visibleFaces[face])
                    {
                        AddFaceMesh(this[x, y, z], face, lightValues[face], blockPosition);
                    }
                }
            }

            Vertices = VertexList.ToArray();
            TransparentVertices = TransparentVertexList.ToArray();

            VertexCount = VertexList.Count;
            TransparentVertexCount = TransparentVertexList.Count;

            VertexList.Clear();
            TransparentVertexList.Clear();

            UpdateMesh = false;
        }

        void AddFaceMesh(ushort? texture, int face, byte light, Vector3 blockPosition)
        {
            var multiface = Assets.Multiface;
            var multifaceBlocks = Assets.MultifaceBlocks;
            var transparent = Assets.TransparentBlocks;

            if (multiface[(int)texture])
            {
                AddData(VertexList,
                    face, light, blockPosition, multifaceBlocks[(ushort)texture][face]);
            }
            else if (transparent[(int)texture])
            {
                AddData(TransparentVertexList, face, light, blockPosition, texture);
            }
            else
            {
                AddData(VertexList, face, light, blockPosition, texture);
            }
        }

        static void AddData(List<VertexPositionTextureLight> vertices, int face, byte light, Vector3 position, ushort? texture)
        {
            int size = 16;
            int textureCount = Assets.BlockTextures.Count;
            for (int i = 0; i < 6; i++)
            {
                VertexPositionTextureLight vertex = Cube.Faces[face][i];
                vertex.Position += position;

                int skylight = (light >> 4) & 0xF;
                int blockLight = light & 0xF;

                vertex.Light = skylight + size * blockLight;

                if (vertex.TextureCoordinate.Y == 0)
                {
                    vertex.TextureCoordinate.Y = (float)texture / textureCount;
                }
                else
                {
                    vertex.TextureCoordinate.Y = (float)(texture + 1) / textureCount;
                }
                vertices.Add(vertex);
            }
        }
    }
}

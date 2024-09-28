﻿using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using SharpCraft.Rendering;
using SharpCraft.Utility;

namespace SharpCraft.World
{
    public sealed partial class Chunk
    {
        public bool UpdateMesh { get; set; }

        public VertexPositionTextureLight[] Vertices;
        public VertexPositionTextureLight[] TransparentVertices;

        public List<VertexPositionTextureLight> VertexList;
        public List<VertexPositionTextureLight> TransparentVertexList;

        public int VertexCount;
        public int TransparentVertexCount;

        public void CalculateMesh(ChunkNeighbors neighbors, Action<int, int, int, bool[], ChunkNeighbors, bool> GetVisibleFaces)
        {
            bool[] visibleFaces = new bool[6];
            byte[] lightValues = new byte[6];

            Vector3 pos = SIZE * new Vector3(Index.X, Index.Y, Index.Z);
            foreach (Vector3I index in activeBlockIndexes)
            {
                int y = index.Y;
                int x = index.X;
                int z = index.Z;

                Vector3 blockPosition = new Vector3(x, y, z) - pos;
                Array.Clear(visibleFaces, 0, 6);
                Array.Clear(lightValues, 0, 6);

                GetVisibleFaces(y, x, z, visibleFaces, neighbors, true);
                GetFacesLight(lightValues, visibleFaces, y, x, z, neighbors);

                for (int face = 0; face < 6; face++)
                {
                    if (visibleFaces[face])
                    {
                        AddFaceMesh(this[x, y, z].Value, (ushort)face, lightValues[face], blockPosition);
                    }
                }
            }

            Vertices = [.. VertexList];
            TransparentVertices = [.. TransparentVertexList];

            VertexCount = VertexList.Count;
            TransparentVertexCount = TransparentVertexList.Count;

            VertexList.Clear();
            TransparentVertexList.Clear();

            UpdateMesh = false;
        }

        void AddFaceMesh(ushort texture, ushort face, byte light, Vector3 blockPosition)
        {
            if (blockMetadata.IsBlockMultiface(texture))
            {
                AddData(VertexList,
                    face, light, blockPosition, blockMetadata.GetMultifaceBlockFace(texture, face));
            }
            else if (blockMetadata.IsBlockTransparent(texture))
            {
                AddData(TransparentVertexList, face, light, blockPosition, texture);
            }
            else
            {
                AddData(VertexList, face, light, blockPosition, texture);
            }
        }

        void AddData(List<VertexPositionTextureLight> vertices, int face, byte light, Vector3 position, ushort texture)
        {
            int size = 16;
            int textureCount = blockMetadata.GetBlocksCount;
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

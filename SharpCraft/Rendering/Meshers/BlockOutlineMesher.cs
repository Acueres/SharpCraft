using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

using SharpCraft.Utilities;

namespace SharpCraft.Rendering.Meshers;

class BlockOutlineMesher
{
    public VertexPositionTextureLight[] Vertices { get; internal set; } = [];

    public void GenerateMesh(FacesState visibleFaces, Vector3 position, Vector3 direction)
    {
        ReadOnlySpan<AxisDirection> faces =
        [
            AxisDirection.Z,
            AxisDirection.Z,
            AxisDirection.Y,
            AxisDirection.Y,
            AxisDirection.X,
            AxisDirection.X
        ];

        List<VertexPositionTextureLight> vertexList = [];
        AxisDirection dominantAxis = Util.GetDominantAxis(direction);
        foreach (Faces face in visibleFaces.GetFaces())
        {
            if (faces[(int)face] != dominantAxis) continue;

            for (int i = 0; i < 6; i++)
            {
                VertexPositionTextureLight vertex = Cube.Faces[(byte)face][i];

                vertex.Position += position;
                vertexList.Add(vertex);
            }
        }

        Vertices = [.. vertexList];
    }

    public void Flush()
    {
        Vertices = [];
    }
}


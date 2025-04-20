using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Xna.Framework;

using SharpCraft.MathUtilities;
using SharpCraft.Utilities;
using SharpCraft.World.Chunks;
using SharpCraft.World.Lighting;
using SharpCraft.World.Blocks;

namespace SharpCraft.Rendering.Meshers;

static class Cube
{
    public static VertexPositionTextureLight[][] Faces { get; }

    static Cube()
    {
        // Front face: z = 1.
        VertexPositionTextureLight[] front = new VertexPositionTextureLight[6];
        front[0].Position = new Vector3(0, 0, 1);
        front[1].Position = new Vector3(0, 1, 1);
        front[2].Position = new Vector3(1, 0, 1);
        front[3].Position = front[1].Position;
        front[4].Position = new Vector3(1, 1, 1);
        front[5].Position = front[2].Position;

        // Back face: z = 0.
        VertexPositionTextureLight[] back = new VertexPositionTextureLight[6];
        back[0].Position = new Vector3(1, 0, 0);
        back[1].Position = new Vector3(1, 1, 0);
        back[2].Position = new Vector3(0, 0, 0);
        back[3].Position = back[1].Position;
        back[4].Position = new Vector3(0, 1, 0);
        back[5].Position = back[2].Position;

        // Top face: y = 1.
        VertexPositionTextureLight[] top = new VertexPositionTextureLight[6];
        top[0].Position = new Vector3(0, 1, 1);
        top[1].Position = new Vector3(0, 1, 0);
        top[2].Position = new Vector3(1, 1, 1);
        top[3].Position = top[1].Position;
        top[4].Position = new Vector3(1, 1, 0);
        top[5].Position = top[2].Position;

        // Bottom face: y = 0.
        VertexPositionTextureLight[] bottom = new VertexPositionTextureLight[6];
        bottom[0].Position = new Vector3(0, 0, 0);
        bottom[1].Position = new Vector3(0, 0, 1);
        bottom[2].Position = new Vector3(1, 0, 0);
        bottom[3].Position = bottom[1].Position;
        bottom[4].Position = new Vector3(1, 0, 1);
        bottom[5].Position = bottom[2].Position;

        // Right face: x = 1.
        VertexPositionTextureLight[] right = new VertexPositionTextureLight[6];
        right[0].Position = new Vector3(1, 0, 1);
        right[1].Position = new Vector3(1, 1, 1);
        right[2].Position = new Vector3(1, 0, 0);
        right[3].Position = right[1].Position;
        right[4].Position = new Vector3(1, 1, 0);
        right[5].Position = right[2].Position;

        // Left face: x = 0.
        VertexPositionTextureLight[] left = new VertexPositionTextureLight[6];
        left[0].Position = new Vector3(0, 0, 0);
        left[1].Position = new Vector3(0, 1, 0);
        left[2].Position = new Vector3(0, 0, 1);
        left[3].Position = left[1].Position;
        left[4].Position = new Vector3(0, 1, 1);
        left[5].Position = left[2].Position;


        Faces = [front, back, top, bottom, right, left];

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
}

class ChunkMesher(BlockMetadataProvider blockMetadata)
{
    readonly BlockMetadataProvider blockMetadata = blockMetadata;
    readonly int blockCount = blockMetadata.BlockCount;

    readonly ConcurrentDictionary<Vec3<int>, VertexPositionTextureLight[]> verticesCache = [];
    readonly ConcurrentDictionary<Vec3<int>, VertexPositionTextureLight[]> transparentVerticesCache = [];

    public VertexPositionTextureLight[] GetVertices(Vec3<int> index)
    {
        return verticesCache[index];
    }

    public VertexPositionTextureLight[] GetTransparentVertices(Vec3<int> index)
    {
        return transparentVerticesCache[index];
    }

    public void AddMesh(Chunk chunk)
    {
        var (vertices, transparentVertices) = BuildMesh(chunk);

        if (!verticesCache.TryAdd(chunk.Index, vertices))
        {
            verticesCache[chunk.Index] = vertices;
        }

        if (!transparentVerticesCache.TryAdd(chunk.Index, transparentVertices))
        {
            transparentVerticesCache[chunk.Index] = transparentVertices;
        }
    }

    public void Remove(Vec3<int> index)
    {
        verticesCache.TryRemove(index, out _);
        transparentVerticesCache.TryRemove(index, out _);
    }

    public (VertexPositionTextureLight[], VertexPositionTextureLight[]) BuildMesh(Chunk chunk)
    {
        List<VertexPositionTextureLight> vertices = [];
        List<VertexPositionTextureLight> transparentVertices = [];

        foreach (Vec3<byte> index in chunk.GetVisibleBlocks())
        {
            int x = index.X;
            int y = index.Y;
            int z = index.Z;

            Vector3 blockPosition = new Vector3(x, y, z) + chunk.Position;

            FacesState visibleFaces = chunk.GetVisibleFaces(index);

            if (!visibleFaces.Any()) continue;

            FacesData<LightValue> lightValues = LightSystem.GetFacesLight(visibleFaces, x, y, z, chunk);
            Block block = chunk[x, y, z];

            foreach (Faces face in visibleFaces.GetFaces())
            {
                LightValue light = lightValues.GetValue(face);

                if (blockMetadata.IsBlockTransparent(block))
                {
                    var blockVerticesTransparent = GetBlockVertices(face, light, blockPosition, block.Value);
                    transparentVertices.AddRange(blockVerticesTransparent);
                }
                else
                {
                    var blockVertices = GetBlockVertices(face, light, blockPosition,
                        blockMetadata.IsBlockMultiface(block) ? blockMetadata.GetMultifaceBlockFace(block, face) : block.Value);
                    vertices.AddRange(blockVertices);
                }
            }
        }

        return ([.. vertices], [.. transparentVertices]);
    }

    VertexPositionTextureLight[] GetBlockVertices(Faces face, LightValue light, Vector3 position, ushort texture)
    {
        const int nVertices = 6;
        VertexPositionTextureLight[] vertices = new VertexPositionTextureLight[nVertices];
        for (int i = 0; i < nVertices; i++)
        {
            VertexPositionTextureLight vertex = Cube.Faces[(byte)face][i];
            vertex.Position += position;

            int skylight = light.SkyValue;
            int blockLight = light.BlockValue;

            vertex.Light = skylight + 397 * blockLight;

            if (vertex.TextureCoordinate.Y == 0)
            {
                vertex.TextureCoordinate.Y = (float)texture / blockCount;
            }
            else
            {
                vertex.TextureCoordinate.Y = (float)(texture + 1) / blockCount;
            }

            vertices[i] = vertex;
        }

        return vertices;
    }
}

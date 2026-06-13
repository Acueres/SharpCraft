using SharpCraft.AssetProcessing;
using SharpCraft.World.Blocks;
using SharpCraft.SharpMath;
using SharpCraft.Rendering;
using SharpCraft.World.Chunks;

using System.Collections.Concurrent;
using System.Numerics;

namespace SharpCraft.World.Meshing;

internal class ChunkMesher(BlockRegistry blockRegistry)
{
    private readonly ConcurrentDictionary<Vec3<int>, VoxelFace[]> verticesCache = [];
    private readonly ConcurrentDictionary<Vec3<int>, VoxelFace[]> transparentVerticesCache = [];

    private const byte Skylight = 15;
    private const byte BlockLight = 0;
    
    public VoxelFace[] GetFaces(in Vec3<int> index)
    {
        return verticesCache[index];
    }

    public VoxelFace[] GetTransparentFaces(in Vec3<int> index)
    {
        return transparentVerticesCache[index];
    }

    public void Build(Chunk chunk)
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

    public (VoxelFace[], VoxelFace[]) BuildMesh(Chunk chunk)
    {
        List<VoxelFace> faces = [];
        List<VoxelFace> transparentFaces = [];

        foreach (Vec3<byte> index in chunk.GetVisibleBlocks())
        {
            int x = index.X;
            int y = index.Y;
            int z = index.Z;

            Vector3 blockPosition = new Vector3(x, y, z) + chunk.Position;

            FacesState visibleFaces = chunk.GetVisibleFaces(index);

            if (!visibleFaces.Any()) continue;

            //FacesData<LightValue> lightValues = LightSystem.GetFacesLight(visibleFaces, x, y, z, chunk);
            Block block = chunk[x, y, z];

            foreach (FaceDirection face in visibleFaces.GetFaces())
            {
                //LightValue light = lightValues.GetValue(face);
                
                var voxelFace = BuildVoxelFace(face, /*light,*/ blockPosition,
                    blockRegistry.GetFaceTextureLayer(block, face));

                if (blockRegistry.IsTransparent(block))
                {
                    transparentFaces.AddRange(voxelFace);
                }
                else
                {
                    faces.AddRange(voxelFace);
                }
            }
        }

        return ([.. faces], [.. transparentFaces]);
    }

    VoxelFace BuildVoxelFace(FaceDirection face, /*LightValue light,*/ Vector3 position, uint textureLayer)
    {
        return new VoxelFace(
            2 * position.X,
            2 * position.Y,
            2 * position.Z,
            (uint)face,
            textureLayer,
            PackLight(Skylight, BlockLight)
        );
    }
    
    private static uint PackLight(byte skylight, byte blockLight)
    {
        return
            ((uint)skylight & 0xF) |
            (((uint)blockLight & 0xF) << 4);
    }
}
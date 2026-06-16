using SharpCraft.World.Blocks;
using SharpCraft.SharpMath;
using SharpCraft.Rendering;
using SharpCraft.World.Chunks;

using System.Collections.Concurrent;
using System.Numerics;

namespace SharpCraft.World.Meshing;

internal class ChunkMesher(BlockRegistry blockRegistry)
{
    private readonly ConcurrentDictionary<Vec3<int>, VoxelFace[]> facesCache = [];
    private readonly ConcurrentDictionary<Vec3<int>, VoxelFace[]> transparentFacesCache = [];

    private const byte Skylight = 15;
    private const byte BlockLight = 0;
    
    public VoxelFace[] GetFaces(in Vec3<int> index)
    {
        return facesCache[index];
    }

    public VoxelFace[] GetTransparentFaces(in Vec3<int> index)
    {
        return transparentFacesCache[index];
    }

    public void Build(Chunk chunk)
    {
        var (faces, transparentFaces) = BuildMesh(chunk);

        facesCache[chunk.Index] = faces;
        transparentFacesCache[chunk.Index] = transparentFaces;
    }

    public void Remove(Vec3<int> index)
    {
        facesCache.TryRemove(index, out _);
        transparentFacesCache.TryRemove(index, out _);
    }

    private (VoxelFace[], VoxelFace[]) BuildMesh(Chunk chunk)
    {
        List<VoxelFace> faces = [];
        List<VoxelFace> transparentFaces = [];

        foreach ((Vec3<byte> index, FacesState visibleFaces) in chunk.GetVisibleBlocks())
        {
            int x = index.X;
            int y = index.Y;
            int z = index.Z;

            Vector3 position = new Vector3(x, y, z) + chunk.Position;

            //FacesData<LightValue> lightValues = LightSystem.GetFacesLight(visibleFaces, x, y, z, chunk);
            Block block = chunk[x, y, z];
            bool transparent = blockRegistry.IsTransparent(block);
            var target = transparent ? transparentFaces : faces;
            
            foreach (FaceDirection face in visibleFaces.GetFaces())
            {
                //LightValue light = lightValues.GetValue(face);
                
                var voxelFace = new VoxelFace(
                    position.X,
                    position.Y,
                    position.Z,
                    (uint)face,
                    blockRegistry.GetFaceTextureLayer(block, face),
                    PackLight(Skylight, BlockLight)
                );
                
                target.Add(voxelFace);
            }
        }

        return ([.. faces], [.. transparentFaces]);
    }
    
    private static uint PackLight(byte skylight, byte blockLight)
    {
        return
            ((uint)skylight & 0xF) |
            (((uint)blockLight & 0xF) << 4);
    }
}
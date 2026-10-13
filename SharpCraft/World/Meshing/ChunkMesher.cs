using SharpCraft.World.Blocks;
using SharpCraft.SharpMath;
using SharpCraft.Rendering;
using SharpCraft.World.Chunks;
using SharpCraft.World.Lighting;

namespace SharpCraft.World.Meshing;

internal class ChunkMesher(BlockRegistry blockRegistry)
{
    public (VoxelFace[] Opaque, VoxelFace[] Transparent) Build(Chunk chunk, in NeighborSet neighbors)
    {
        List<VoxelFace> opaque = [];
        List<VoxelFace> transparent = [];

        foreach ((Vec3<byte> blockIndex, FacesState visibleFaces) in chunk.GetVisibleBlocks(neighbors))
        {
            int x = blockIndex.X;
            int y = blockIndex.Y;
            int z = blockIndex.Z;

            //Vector3 position = new Vector3(x, y, z) + chunk.Position;

            FacesData<LightValue> lightValues = chunk.Light!.GetFacesLight(visibleFaces, neighbors, x, y, z);
            Block block = chunk[x, y, z];
            bool isTransparent = blockRegistry.IsTransparent(block);
            var target = isTransparent ? transparent : opaque;

            foreach (FaceDirection face in visibleFaces.GetFaces())
            {
                LightValue light = lightValues.GetValue(face);

                var voxelFace = new VoxelFace(
                    blockIndex,
                    (byte)face,
                    (ushort)blockRegistry.GetFaceTextureLayer(block, face),
                    light.Value
                );

                target.Add(voxelFace);
            }
        }

        return ([.. opaque], [.. transparent]);
    }
}
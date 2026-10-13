using SharpCraft.Rendering;
using SharpCraft.SharpMath;

namespace SharpCraft.World.Meshing;

internal enum MeshUpdateState
{
    Updated,
    Obsolete
}

internal record MeshUpdate(Vec3<int> Index, MeshUpdateState State, VoxelFace[] Opaque, VoxelFace[] Transparent);
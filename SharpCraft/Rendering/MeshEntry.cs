using SharpCraft.SharpMath;

namespace SharpCraft.Rendering;

internal record MeshEntry(Vec3<int> Index, VoxelFace[] Opaque, VoxelFace[] Transparent);
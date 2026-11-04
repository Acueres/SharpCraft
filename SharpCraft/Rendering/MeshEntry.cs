using SharpCraft.Graphics.Resources;

namespace SharpCraft.Rendering;

internal record MeshEntry(List<GpuAllocation> OpaqueAllocations, List<GpuAllocation> TransparentAllocations,
    uint OpaqueCount,  uint TransparentCount);
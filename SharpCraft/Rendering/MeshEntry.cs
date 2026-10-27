using SharpCraft.Graphics.Resources;

namespace SharpCraft.Rendering;

internal record MeshEntry(List<CpuAllocation> OpaqueAllocations, List<CpuAllocation> TransparentAllocations,
    uint OpaqueCount,  uint TransparentCount);
using SharpCraft.SharpMath;

namespace SharpCraft.Graphics.Resources;

internal class GpuAllocation
{
    public Page Page { get; }
    public CpuAllocation CpuAllocation { get; }
    public uint SlotId { get; }

    public GpuAllocation(Page page, uint slotId, CpuAllocation cpuAllocation)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(cpuAllocation);

        if (slotId > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(slotId), slotId, "Slot id must fit into ushort.");
        }

        Page = page;
        CpuAllocation = cpuAllocation;
        SlotId = slotId;
    }
}
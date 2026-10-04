using SharpCraft.SharpMath;

using System.Runtime.InteropServices;

namespace SharpCraft.Rendering;

/// <summary>
/// Generalized axis-aligned quad: 16 bytes, stored as four uints.
/// </summary>
/// <code>
/// Word   Bits     Field
/// uint0   0–7     anchorX       (8 bits)
///         8–15    anchorY       (8 bits)
///        16–23    anchorZ       (8 bits)
///        24–26    direction     (3 bits)
///        27–31    flags         (5 bits)
///
/// uint1   0–7     widthMinus1   (8 bits)
///         8–15    heightMinus1  (8 bits)
///        16–19    uvOriginU     (4 bits)
///        20–23    uvOriginV     (4 bits)
///        24–31    flagsExt      (8 bits)
///
/// uint2   0–15    textureId    (16 bits)
///        16–31    slotId       (16 bits)
///
/// uint3   0–3     skylight      (4 bits)
///         4–7     blockR        (4 bits)
///         8–11    blockG        (4 bits)
///        12–15    blockB        (4 bits)
///        16–31    spare        (16 bits, reserved)
/// </code>
/// Geometry uses a 1/16-block lattice: 16 micro-units per block.
/// Anchors range from 0 to 255 on each axis.
/// On the normal axis, a negative-facing face lies at anchor;
/// a positive-facing face lies at anchor + 1.
/// Thus anchor 255 can represent the positive boundary plane 256.
/// Tangent-axis placement, winding and UV orientation follow the
/// shared direction-to-normal/U/V basis convention.
///
/// Width and height decode as stored value + 1, giving 1–256
/// micro-units along the face's U and V tangent axes.
/// A full block face therefore has dimensions 16 by 16.
///
/// UV origins use 1/16-tile units. Sampling supports repeat mode
/// for terrain and sub-rectangle mode for model/detail faces.
/// textureId is an abstract engine texture ID.
/// slotId indexes the slot table for the currently bound page.
///
/// Planned flags: decor, displaced, doubleSided,
/// alphaTest, uvSubRect.
/// flagsExt: UV rotation (2 bits: 0/90/180/270 degrees),
/// flip U (1 bit), flip V (1 bit), reserved (4 bits).
///
/// Lattice coordinates must be expanded using the engine's chosen
/// chunk-local origin convention to preserve world placement.
[StructLayout(LayoutKind.Sequential)]
internal struct VoxelFace
{
    public uint Uint0;
    public uint Uint1;
    public uint Uint2;
    public uint Uint3;

    public VoxelFace(
        Vec3<byte> blockIndex,
        byte direction,
        ushort textureId,
        ushort packedLight)
    {
        Uint0 = ((uint)direction << 24) | ((uint)blockIndex.Z << 16) |
                ((uint)blockIndex.Y << 8) | blockIndex.X;
        Uint2 = textureId;
        Uint3 = packedLight;
    }

    public void SetSlotId(ushort slotId)
    {
        Uint2 = (Uint2 & 0x0000FFFFu) | ((uint)slotId << 16);
    }
}
using System.Globalization;
using System.Numerics;
using SharpCraft.SharpMath;

namespace SharpCraft.Diagnostics;

internal static class CompactProfileText
{
    public static string Frame(in FrameProfile profile) =>
        FormattableString.Invariant($"{profile.Fps:0} FPS · {profile.FrameMilliseconds:0.0} ms");

    public static string Position(Vector3 position) =>
        FormattableString.Invariant($"{position.X:0.0}  {position.Y:0.0}  {position.Z:0.0}");

    public static string ChunkIndex(Vec3<int> index) =>
        FormattableString.Invariant($"{index.X}  {index.Y}  {index.Z}");

    public static string Count(ulong count) => count switch
    {
        >= 1_000_000_000 => (count / 1_000_000_000.0).ToString("0.0", CultureInfo.InvariantCulture) + "B",
        >= 1_000_000 => (count / 1_000_000.0).ToString("0.0", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (count / 1_000.0).ToString("0.0", CultureInfo.InvariantCulture) + "K",
        _ => count.ToString(CultureInfo.InvariantCulture)
    };

    public static string Truncate(string text, int maxCharacters) =>
        text.Length <= maxCharacters ? text : text[..(maxCharacters - 1)] + "…";
}

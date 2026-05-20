using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpCraft.Graphics;

// ─────────────────────────────────────────────────────────────────────────────
// Color enum
// ─────────────────────────────────────────────────────────────────────────────
 
/// <summary>
/// Named color identifiers. Use <see cref="Colors.Get"/> or the corresponding
/// <see cref="Colors"/> field to retrieve the <see cref="Rgba"/> value.
/// </summary>
public enum Color
{
    Transparent = 0,
    AliceBlue,
    Amber,
    Amethyst,
    AntiqueWhite,
    Apricot,
    Aqua,
    Aquamarine,
    Auburn,
    Azure,
    Beige,
    Bisque,
    Black,
    BlanchedAlmond,
    Blue,
    BlueViolet,
    Brown,
    Burgundy,
    BurlyWood,
    CadetBlue,
    Carmine,
    Cerulean,
    Champagne,
    Charcoal,
    Chartreuse,
    Chocolate,
    Cobalt,
    Copper,
    Coral,
    CornflowerBlue,
    Cornsilk,
    Cream,
    Crimson,
    Cyan,
    DarkBlue,
    DarkCyan,
    DarkGoldenrod,
    DarkGray,
    DarkGreen,
    DarkKhaki,
    DarkMagenta,
    DarkOliveGreen,
    DarkOrange,
    DarkOrchid,
    DarkRed,
    DarkSalmon,
    DarkSeaGreen,
    DarkSlateBlue,
    DarkSlateGray,
    DarkTurquoise,
    DarkViolet,
    DeepPink,
    DeepSkyBlue,
    DimGray,
    DodgerBlue,
    Ecru,
    Emerald,
    Fern,
    Firebrick,
    Flax,
    FloralWhite,
    ForestGreen,
    Fuchsia,
    Gainsboro,
    GhostWhite,
    Gold,
    Goldenrod,
    Gray,
    Green,
    GreenYellow,
    Honeydew,
    HotPink,
    IndianRed,
    Indigo,
    Ivory,
    Jade,
    Khaki,
    Lavender,
    LavenderBlush,
    LawnGreen,
    LemonChiffon,
    Lilac,
    LightBlue,
    LightCoral,
    LightCyan,
    LightGoldenrodYellow,
    LightGray,
    LightGreen,
    LightPink,
    LightSalmon,
    LightSeaGreen,
    LightSkyBlue,
    LightSlateGray,
    LightSteelBlue,
    LightYellow,
    Lime,
    LimeGreen,
    Linen,
    Magenta,
    Mahogany,
    Maroon,
    Marigold,
    Mauve,
    MediumAquamarine,
    MediumBlue,
    MediumOrchid,
    MediumPurple,
    MediumSeaGreen,
    MediumSlateBlue,
    MediumSpringGreen,
    MediumTurquoise,
    MediumVioletRed,
    MidnightBlue,
    Mint,
    MintCream,
    MistyRose,
    Moccasin,
    Moss,
    Mustard,
    NavajoWhite,
    Navy,
    Ochre,
    OldLace,
    Olive,
    OliveDrab,
    Onyx,
    Orange,
    OrangeRed,
    Orchid,
    PaleGoldenrod,
    PaleGreen,
    PaleTurquoise,
    PaleVioletRed,
    PapayaWhip,
    PeachPuff,
    Pear,
    Periwinkle,
    Peru,
    Pink,
    Plum,
    PowderBlue,
    Puce,
    Purple,
    Red,
    RosyBrown,
    RoyalBlue,
    Rust,
    SaddleBrown,
    Saffron,
    Sage,
    Salmon,
    SandyBrown,
    Sapphire,
    Scarlet,
    SeaGreen,
    SeaShell,
    Sepia,
    Sienna,
    Silver,
    SkyBlue,
    SlateBlue,
    SlateGray,
    Snow,
    SpringGreen,
    SteelBlue,
    Tan,
    Tangerine,
    Taupe,
    Teal,
    Thistle,
    Tomato,
    Turquoise,
    Umber,
    Vermillion,
    Violet,
    Wheat,
    White,
    WhiteSmoke,
    Wisteria,
    Yellow,
    YellowGreen,
}
 
// ─────────────────────────────────────────────────────────────────────────────
// Rgba struct
// ─────────────────────────────────────────────────────────────────────────────
 
/// <summary>
/// A 32-bit packed RGBA color. Bit layout: <c>[ A(31-24) | B(23-16) | G(15-8) | R(7-0) ]</c>.
/// R lives in the least-significant byte, matching SDL_Color's in-memory order on
/// little-endian hardware.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Rgba : IEquatable<Rgba>
{
    private readonly uint packed;
 
    // ── Constructors ──────────────────────────────────────────────────────────
 
    /// <summary>Wraps a raw packed uint (ABGR layout) with no conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba(uint packed) => this.packed = packed;
 
    /// <summary>
    /// Constructs from byte components. No clamping — fastest overload.
    /// Alpha defaults to 255 (fully opaque).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba(byte r, byte g, byte b, byte a = 255)
        => packed = ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
 
    /// <summary>
    /// Constructs from integer components (0–255). Values outside [0, 255] are clamped.
    /// Alpha defaults to 255 (fully opaque).
    /// </summary>
    public Rgba(int r, int g, int b, int a = 255)
    {
        var cr = (uint)Math.Clamp(r, 0, 255);
        var cg = (uint)Math.Clamp(g, 0, 255);
        var cb = (uint)Math.Clamp(b, 0, 255);
        var ca = (uint)Math.Clamp(a, 0, 255);
        packed = (ca << 24) | (cb << 16) | (cg << 8) | cr;
    }
 
    /// <summary>
    /// Constructs from float components in the [0, 1] range. Values are clamped.
    /// Alpha defaults to 1.0 (fully opaque).
    /// </summary>
    public Rgba(float r, float g, float b, float a = 1f)
        : this(
            (int)MathF.Round(r * 255),
            (int)MathF.Round(g * 255),
            (int)MathF.Round(b * 255),
            (int)MathF.Round(a * 255))
    { }
 
    /// <summary>Constructs from a <see cref="Vector3"/> (RGB). Alpha is set to 255.</summary>
    public Rgba(Vector3 v) : this(v.X, v.Y, v.Z) { }
 
    /// <summary>Constructs from a <see cref="Vector4"/> (XYZW → RGBA).</summary>
    public Rgba(Vector4 v) : this(v.X, v.Y, v.Z, v.W) { }
 
    // ── Non-mutating "with" helpers ───────────────────────────────────────────
 
    /// <summary>Returns a copy of this color with a replaced red channel.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba WithR(byte r) => new((packed & 0xFFFFFF00u) | r);
 
    /// <summary>Returns a copy of this color with a replaced green channel.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba WithG(byte g) => new((packed & 0xFFFF00FFu) | ((uint)g << 8));
 
    /// <summary>Returns a copy of this color with a replaced blue channel.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba WithB(byte b) => new((packed & 0xFF00FFFFu) | ((uint)b << 16));
 
    /// <summary>Returns a copy of this color with a replaced alpha channel (0–255).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba WithA(byte a) => new((packed & 0x00FFFFFFu) | ((uint)a << 24));
 
    /// <summary>Returns a copy of this color with a replaced alpha channel (0–1, clamped).</summary>
    public Rgba WithA(float a)
        => WithA((byte)Math.Clamp((int)(a * 255), 0, 255));
 
    // ── Components ────────────────────────────────────────────────────────────
 
    /// <summary>Red component (0–255).</summary>
    public byte R
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte)packed;
    }
 
    /// <summary>Green component (0–255).</summary>
    public byte G
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte)(packed >> 8);
    }
 
    /// <summary>Blue component (0–255).</summary>
    public byte B
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte)(packed >> 16);
    }
 
    /// <summary>Alpha component (0–255). 0 = fully transparent, 255 = fully opaque.</summary>
    public byte A
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (byte)(packed >> 24);
    }
 
    /// <summary>Raw packed value in <c>[ A | B | G | R ]</c> (ABGR) layout.</summary>
    public uint PackedValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => packed;
    }
 
    /// <summary><c>true</c> if the alpha component is 255.</summary>
    public bool IsOpaque
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => A == 255;
    }
 
    /// <summary><c>true</c> if the alpha component is 0.</summary>
    public bool IsTransparent
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => A == 0;
    }
 
    // ── Conversion ────────────────────────────────────────────────────────────
 
    /// <summary>Returns RGB in the [0, 1] range as a <see cref="Vector3"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 ToVector3() => new(R / 255f, G / 255f, B / 255f);
 
    /// <summary>Returns RGBA in the [0, 1] range as a <see cref="Vector4"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 ToVector4() => new(R / 255f, G / 255f, B / 255f, A / 255f);
 
    /// <summary>Returns a CSS-style <c>#RRGGBB</c> hex string (no alpha).</summary>
    public string ToHexRGB() => $"#{R:X2}{G:X2}{B:X2}";
 
    /// <summary>Returns a CSS-style <c>#RRGGBBAA</c> hex string.</summary>
    public string ToHexRGBA() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";
 
    /// <summary>
    /// Tries to parse a hex color string into an <see cref="Rgba"/>.
    /// Accepts <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c>
    /// (with or without the leading <c>#</c>).
    /// </summary>
    public static bool TryParseHex(ReadOnlySpan<char> input, out Rgba result)
    {
        result = default;
        var hex = input is ['#', ..] ? input[1..] : input;
 
        static int Nibble(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _                 => -1,
        };
 
        static bool TryByte(ReadOnlySpan<char> s, out byte v)
        {
            v = 0;
            if (s.Length != 2) return false;
            int hi = Nibble(s[0]), lo = Nibble(s[1]);
            if (hi < 0 || lo < 0) return false;
            v = (byte)((hi << 4) | lo);
            return true;
        }
 
        switch (hex.Length)
        {
            case 3:
            {
                int r = Nibble(hex[0]), g = Nibble(hex[1]), b = Nibble(hex[2]);
                if (r < 0 || g < 0 || b < 0) return false;
                result = new Rgba((byte)(r * 17), (byte)(g * 17), (byte)(b * 17));
                return true;
            }
            case 4:
            {
                int r = Nibble(hex[0]), g = Nibble(hex[1]), b = Nibble(hex[2]), a = Nibble(hex[3]);
                if (r < 0 || g < 0 || b < 0 || a < 0) return false;
                result = new Rgba((byte)(r * 17), (byte)(g * 17), (byte)(b * 17), (byte)(a * 17));
                return true;
            }
            case 6:
                if (!TryByte(hex[..2], out byte r6) || !TryByte(hex[2..4], out byte g6) ||
                    !TryByte(hex[4..],  out byte b6)) return false;
                result = new Rgba(r6, g6, b6);
                return true;
            case 8:
                if (!TryByte(hex[..2], out byte r8) || !TryByte(hex[2..4], out byte g8) ||
                    !TryByte(hex[4..6], out byte b8) || !TryByte(hex[6..], out byte a8)) return false;
                result = new Rgba(r8, g8, b8, a8);
                return true;
            default:
                return false;
        }
    }
 
    // ── Color-space operations ────────────────────────────────────────────────
 
    /// <summary>
    /// Converts this color to HSL components.
    /// </summary>
    /// <param name="h">Hue in [0, 360).</param>
    /// <param name="s">Saturation in [0, 1].</param>
    /// <param name="l">Lightness in [0, 1].</param>
    public void ToHSL(out float h, out float s, out float l)
    {
        float r = R / 255f, g = G / 255f, b = B / 255f;
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;
 
        l = (max + min) * 0.5f;
 
        if (delta == 0f) { h = s = 0f; return; }
 
        s = delta / (1f - MathF.Abs(2f * l - 1f));
        h = max == r ? 60f * (((g - b) / delta) % 6f)
          : max == g ? 60f * ((b - r) / delta + 2f)
          :            60f * ((r - g) / delta + 4f);
        if (h < 0f) h += 360f;
    }
 
    /// <summary>
    /// Converts this color to HSV components.
    /// </summary>
    /// <param name="h">Hue in [0, 360).</param>
    /// <param name="s">Saturation in [0, 1].</param>
    /// <param name="v">Value in [0, 1].</param>
    public void ToHSV(out float h, out float s, out float v)
    {
        float r = R / 255f, g = G / 255f, b = B / 255f;
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;
 
        v = max;
        s = max == 0f ? 0f : delta / max;
 
        if (delta == 0f) { h = 0f; return; }
 
        h = max == r ? 60f * (((g - b) / delta) % 6f)
          : max == g ? 60f * ((b - r) / delta + 2f)
          :            60f * ((r - g) / delta + 4f);
        if (h < 0f) h += 360f;
    }
 
    /// <summary>Creates an <see cref="Rgba"/> from HSL components.</summary>
    /// <param name="h">Hue in [0, 360).</param>
    /// <param name="s">Saturation in [0, 1].</param>
    /// <param name="l">Lightness in [0, 1].</param>
    public static Rgba FromHSL(float h, float s, float l)
    {
        if (s == 0f)
        {
            var mono = (byte)MathF.Round(l * 255f);
            return new Rgba(mono, mono, mono);
        }
 
        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;
        float hn = ((h % 360f) + 360f) % 360f / 360f;
 
        return new Rgba(
            Channel(p, q, hn + 1f / 3f),
            Channel(p, q, hn),
            Channel(p, q, hn - 1f / 3f));
 
        static float Channel(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 0.5f)    return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
    }
 
    /// <summary>Creates an <see cref="Rgba"/> from HSV components.</summary>
    /// <param name="h">Hue in [0, 360).</param>
    /// <param name="s">Saturation in [0, 1].</param>
    /// <param name="v">Value in [0, 1].</param>
    public static Rgba FromHSV(float h, float s, float v)
    {
        if (s == 0f)
        {
            var mono = (byte)MathF.Round(v * 255f);
            return new Rgba(mono, mono, mono);
        }
 
        h = ((h % 360f) + 360f) % 360f;
        int   sector = (int)(h / 60f) % 6;
        float frac   = h / 60f - MathF.Floor(h / 60f);
        float p = v * (1f - s);
        float q = v * (1f - s * frac);
        float t = v * (1f - s * (1f - frac));
 
        var (r, g, b) = sector switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
 
        return new Rgba(r, g, b);
    }
 
    // ── Static operations ─────────────────────────────────────────────────────
 
    /// <summary>Linearly interpolates between two colors component-wise. <paramref name="t"/> is clamped to [0, 1].</summary>
    public static Rgba Lerp(Rgba a, Rgba b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgba(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }
 
    /// <summary>Scales all RGBA components by a scalar. Result is clamped to [0, 255].</summary>
    public static Rgba Multiply(Rgba c, float scale)
        => new((int)(c.R * scale), (int)(c.G * scale), (int)(c.B * scale), (int)(c.A * scale));
 
    /// <summary>Scales only the alpha channel by a scalar. Result is clamped to [0, 255].</summary>
    public static Rgba MultiplyAlpha(Rgba c, float scale)
        => c.WithA((byte)Math.Clamp((int)(c.A * scale), 0, 255));
 
    /// <summary>Converts a straight-alpha color to premultiplied alpha.</summary>
    public static Rgba Premultiply(Rgba c)
        => new(c.R * c.A / 255, c.G * c.A / 255, c.B * c.A / 255, c.A);
 
    // ── Operators ─────────────────────────────────────────────────────────────
 
    /// <inheritdoc cref="Multiply(Rgba, float)"/>
    public static Rgba operator *(Rgba c, float s) => Multiply(c, s);
 
    /// <inheritdoc cref="Multiply(Rgba, float)"/>
    public static Rgba operator *(float s, Rgba c) => Multiply(c, s);
 
    /// <summary>Component-wise multiplication; 255 × 255 stays 255.</summary>
    public static Rgba operator *(Rgba a, Rgba b) => new(
        a.R * b.R / 255,
        a.G * b.G / 255,
        a.B * b.B / 255,
        a.A * b.A / 255);
 
    public static bool operator ==(Rgba a, Rgba b) => a.packed == b.packed;
    public static bool operator !=(Rgba a, Rgba b) => a.packed != b.packed;
 
    /// <summary>Implicitly converts a <see cref="Vector4"/> (XYZW) to an <see cref="Rgba"/> (RGBA).</summary>
    public static implicit operator Rgba(Vector4 v) => new(v.X, v.Y, v.Z, v.W);
 
    /// <summary>Implicitly converts an <see cref="Rgba"/> to a <see cref="Vector4"/> (XYZW).</summary>
    public static implicit operator Vector4(Rgba c) => c.ToVector4();
 
    // ── Deconstruct ───────────────────────────────────────────────────────────
 
    /// <summary>Deconstructs RGB channels as bytes.</summary>
    public void Deconstruct(out byte r, out byte g, out byte b)
        => (r, g, b) = (R, G, B);
 
    /// <summary>Deconstructs RGBA channels as bytes.</summary>
    public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
        => (r, g, b, a) = (R, G, B, A);
 
    /// <summary>Deconstructs RGB channels as normalized floats [0, 1].</summary>
    public void Deconstruct(out float r, out float g, out float b)
        => (r, g, b) = (R / 255f, G / 255f, B / 255f);
 
    /// <summary>Deconstructs RGBA channels as normalized floats [0, 1].</summary>
    public void Deconstruct(out float r, out float g, out float b, out float a)
        => (r, g, b, a) = (R / 255f, G / 255f, B / 255f, A / 255f);
 
    // ── Equality & formatting ─────────────────────────────────────────────────
 
    /// <inheritdoc />
    public bool Equals(Rgba other) => packed == other.packed;
 
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Rgba other && Equals(other);
 
    /// <inheritdoc />
    public override int GetHashCode() => (int)packed;
 
    /// <inheritdoc />
    public override string ToString() => $"{{R:{R} G:{G} B:{B} A:{A}}}";
}
 
// ─────────────────────────────────────────────────────────────────────────────
// Colors static provider
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Provides a <see cref="static"/> <see cref="readonly"/> <see cref="Rgba"/> field for every
/// named <see cref="Color"/>, plus an O(1) <see cref="Get"/> lookup and a
/// <see cref="FindNearest"/> search.
/// </summary>
/// <remarks>
/// Field declaration order MUST stay in sync with the <see cref="Color"/> enum so that
/// the <c>_lookup</c> array remains consistent.
/// </remarks>
public static class Colors
{
    // ── Named fields (181 colors) ─────────────────────────────────────────────
    //    RGB values are standard CSS/X11 unless noted.

    /// <summary>Transparent (R:0 G:0 B:0 A:0)</summary>
    public static readonly Rgba Transparent = new(0, 0, 0, 0);

    /// <summary>AliceBlue (R:240 G:248 B:255)</summary>
    public static readonly Rgba AliceBlue = new(240, 248, 255);

    /// <summary>Amber (R:255 G:191 B:0)</summary>
    public static readonly Rgba Amber = new(255, 191, 0);

    /// <summary>Amethyst (R:153 G:102 B:204)</summary>
    public static readonly Rgba Amethyst = new(153, 102, 204);

    /// <summary>AntiqueWhite (R:250 G:235 B:215)</summary>
    public static readonly Rgba AntiqueWhite = new(250, 235, 215);

    /// <summary>Apricot (R:251 G:206 B:177)</summary>
    public static readonly Rgba Apricot = new(251, 206, 177);

    /// <summary>Aqua (R:0 G:255 B:255)</summary>
    public static readonly Rgba Aqua = new(0, 255, 255);

    /// <summary>Aquamarine (R:127 G:255 B:212)</summary>
    public static readonly Rgba Aquamarine = new(127, 255, 212);

    /// <summary>Auburn — a dark reddish-brown distinct from Brown (R:146 G:43 B:33)</summary>
    public static readonly Rgba Auburn = new(146, 43, 33);

    /// <summary>Azure (R:240 G:255 B:255)</summary>
    public static readonly Rgba Azure = new(240, 255, 255);

    /// <summary>Beige (R:245 G:245 B:220)</summary>
    public static readonly Rgba Beige = new(245, 245, 220);

    /// <summary>Bisque (R:255 G:228 B:196)</summary>
    public static readonly Rgba Bisque = new(255, 228, 196);

    /// <summary>Black (R:0 G:0 B:0)</summary>
    public static readonly Rgba Black = new(0, 0, 0);

    /// <summary>BlanchedAlmond (R:255 G:235 B:205)</summary>
    public static readonly Rgba BlanchedAlmond = new(255, 235, 205);

    /// <summary>Blue (R:0 G:0 B:255)</summary>
    public static readonly Rgba Blue = new(0, 0, 255);

    /// <summary>BlueViolet (R:138 G:43 B:226)</summary>
    public static readonly Rgba BlueViolet = new(138, 43, 226);

    /// <summary>Brown (R:165 G:42 B:42)</summary>
    public static readonly Rgba Brown = new(165, 42, 42);

    /// <summary>Burgundy — a dark wine red (R:128 G:0 B:32)</summary>
    public static readonly Rgba Burgundy = new(128, 0, 32);

    /// <summary>BurlyWood (R:222 G:184 B:135)</summary>
    public static readonly Rgba BurlyWood = new(222, 184, 135);

    /// <summary>CadetBlue (R:95 G:158 B:160)</summary>
    public static readonly Rgba CadetBlue = new(95, 158, 160);

    /// <summary>Carmine — a deep red-crimson (R:150 G:0 B:24)</summary>
    public static readonly Rgba Carmine = new(150, 0, 24);

    /// <summary>Cerulean — a sky-blue (R:42 G:82 B:190)</summary>
    public static readonly Rgba Cerulean = new(42, 82, 190);

    /// <summary>Champagne (R:247 G:231 B:206)</summary>
    public static readonly Rgba Champagne = new(247, 231, 206);

    /// <summary>Charcoal (R:54 G:69 B:79)</summary>
    public static readonly Rgba Charcoal = new(54, 69, 79);

    /// <summary>Chartreuse (R:127 G:255 B:0)</summary>
    public static readonly Rgba Chartreuse = new(127, 255, 0);

    /// <summary>Chocolate (R:210 G:105 B:30)</summary>
    public static readonly Rgba Chocolate = new(210, 105, 30);

    /// <summary>Cobalt (R:0 G:71 B:171)</summary>
    public static readonly Rgba Cobalt = new(0, 71, 171);

    /// <summary>Copper (R:184 G:115 B:51)</summary>
    public static readonly Rgba Copper = new(184, 115, 51);

    /// <summary>Coral (R:255 G:127 B:80)</summary>
    public static readonly Rgba Coral = new(255, 127, 80);

    /// <summary>CornflowerBlue (R:100 G:149 B:237)</summary>
    public static readonly Rgba CornflowerBlue = new(100, 149, 237);

    /// <summary>Cornsilk (R:255 G:248 B:220)</summary>
    public static readonly Rgba Cornsilk = new(255, 248, 220);

    /// <summary>Cream (R:255 G:253 B:208)</summary>
    public static readonly Rgba Cream = new(255, 253, 208);

    /// <summary>Crimson (R:220 G:20 B:60)</summary>
    public static readonly Rgba Crimson = new(220, 20, 60);

    /// <summary>Cyan (R:0 G:255 B:255)</summary>
    public static readonly Rgba Cyan = new(0, 255, 255);

    /// <summary>DarkBlue (R:0 G:0 B:139)</summary>
    public static readonly Rgba DarkBlue = new(0, 0, 139);

    /// <summary>DarkCyan (R:0 G:139 B:139)</summary>
    public static readonly Rgba DarkCyan = new(0, 139, 139);

    /// <summary>DarkGoldenrod (R:184 G:134 B:11)</summary>
    public static readonly Rgba DarkGoldenrod = new(184, 134, 11);

    /// <summary>DarkGray (R:169 G:169 B:169)</summary>
    public static readonly Rgba DarkGray = new(169, 169, 169);

    /// <summary>DarkGreen (R:0 G:100 B:0)</summary>
    public static readonly Rgba DarkGreen = new(0, 100, 0);

    /// <summary>DarkKhaki (R:189 G:183 B:107)</summary>
    public static readonly Rgba DarkKhaki = new(189, 183, 107);

    /// <summary>DarkMagenta (R:139 G:0 B:139)</summary>
    public static readonly Rgba DarkMagenta = new(139, 0, 139);

    /// <summary>DarkOliveGreen (R:85 G:107 B:47)</summary>
    public static readonly Rgba DarkOliveGreen = new(85, 107, 47);

    /// <summary>DarkOrange (R:255 G:140 B:0)</summary>
    public static readonly Rgba DarkOrange = new(255, 140, 0);

    /// <summary>DarkOrchid (R:153 G:50 B:204)</summary>
    public static readonly Rgba DarkOrchid = new(153, 50, 204);

    /// <summary>DarkRed (R:139 G:0 B:0)</summary>
    public static readonly Rgba DarkRed = new(139, 0, 0);

    /// <summary>DarkSalmon (R:233 G:150 B:122)</summary>
    public static readonly Rgba DarkSalmon = new(233, 150, 122);

    /// <summary>DarkSeaGreen (R:143 G:188 B:139)</summary>
    public static readonly Rgba DarkSeaGreen = new(143, 188, 139);

    /// <summary>DarkSlateBlue (R:72 G:61 B:139)</summary>
    public static readonly Rgba DarkSlateBlue = new(72, 61, 139);

    /// <summary>DarkSlateGray (R:47 G:79 B:79)</summary>
    public static readonly Rgba DarkSlateGray = new(47, 79, 79);

    /// <summary>DarkTurquoise (R:0 G:206 B:209)</summary>
    public static readonly Rgba DarkTurquoise = new(0, 206, 209);

    /// <summary>DarkViolet (R:148 G:0 B:211)</summary>
    public static readonly Rgba DarkViolet = new(148, 0, 211);

    /// <summary>DeepPink (R:255 G:20 B:147)</summary>
    public static readonly Rgba DeepPink = new(255, 20, 147);

    /// <summary>DeepSkyBlue (R:0 G:191 B:255)</summary>
    public static readonly Rgba DeepSkyBlue = new(0, 191, 255);

    /// <summary>DimGray (R:105 G:105 B:105)</summary>
    public static readonly Rgba DimGray = new(105, 105, 105);

    /// <summary>DodgerBlue (R:30 G:144 B:255)</summary>
    public static readonly Rgba DodgerBlue = new(30, 144, 255);

    /// <summary>Ecru — an off-white with a slight yellow-grey tint (R:194 G:178 B:128)</summary>
    public static readonly Rgba Ecru = new(194, 178, 128);

    /// <summary>Emerald (R:80 G:200 B:120)</summary>
    public static readonly Rgba Emerald = new(80, 200, 120);

    /// <summary>Fern (R:79 G:121 B:66)</summary>
    public static readonly Rgba Fern = new(79, 121, 66);

    /// <summary>Firebrick (R:178 G:34 B:34)</summary>
    public static readonly Rgba Firebrick = new(178, 34, 34);

    /// <summary>Flax — a light yellowish-tan (R:238 G:220 B:130)</summary>
    public static readonly Rgba Flax = new(238, 220, 130);

    /// <summary>FloralWhite (R:255 G:250 B:240)</summary>
    public static readonly Rgba FloralWhite = new(255, 250, 240);

    /// <summary>ForestGreen (R:34 G:139 B:34)</summary>
    public static readonly Rgba ForestGreen = new(34, 139, 34);

    /// <summary>Fuchsia (R:255 G:0 B:255)</summary>
    public static readonly Rgba Fuchsia = new(255, 0, 255);

    /// <summary>Gainsboro (R:220 G:220 B:220)</summary>
    public static readonly Rgba Gainsboro = new(220, 220, 220);

    /// <summary>GhostWhite (R:248 G:248 B:255)</summary>
    public static readonly Rgba GhostWhite = new(248, 248, 255);

    /// <summary>Gold (R:255 G:215 B:0)</summary>
    public static readonly Rgba Gold = new(255, 215, 0);

    /// <summary>Goldenrod (R:218 G:165 B:32)</summary>
    public static readonly Rgba Goldenrod = new(218, 165, 32);

    /// <summary>Gray (R:128 G:128 B:128)</summary>
    public static readonly Rgba Gray = new(128, 128, 128);

    /// <summary>Green (R:0 G:128 B:0)</summary>
    public static readonly Rgba Green = new(0, 128, 0);

    /// <summary>GreenYellow (R:173 G:255 B:47)</summary>
    public static readonly Rgba GreenYellow = new(173, 255, 47);

    /// <summary>Honeydew (R:240 G:255 B:240)</summary>
    public static readonly Rgba Honeydew = new(240, 255, 240);

    /// <summary>HotPink (R:255 G:105 B:180)</summary>
    public static readonly Rgba HotPink = new(255, 105, 180);

    /// <summary>IndianRed (R:205 G:92 B:92)</summary>
    public static readonly Rgba IndianRed = new(205, 92, 92);

    /// <summary>Indigo (R:75 G:0 B:130)</summary>
    public static readonly Rgba Indigo = new(75, 0, 130);

    /// <summary>Ivory (R:255 G:255 B:240)</summary>
    public static readonly Rgba Ivory = new(255, 255, 240);

    /// <summary>Jade (R:0 G:168 B:107)</summary>
    public static readonly Rgba Jade = new(0, 168, 107);

    /// <summary>Khaki (R:240 G:230 B:140)</summary>
    public static readonly Rgba Khaki = new(240, 230, 140);

    /// <summary>Lavender (R:230 G:230 B:250)</summary>
    public static readonly Rgba Lavender = new(230, 230, 250);

    /// <summary>LavenderBlush (R:255 G:240 B:245)</summary>
    public static readonly Rgba LavenderBlush = new(255, 240, 245);

    /// <summary>LawnGreen (R:124 G:252 B:0)</summary>
    public static readonly Rgba LawnGreen = new(124, 252, 0);

    /// <summary>LemonChiffon (R:255 G:250 B:205)</summary>
    public static readonly Rgba LemonChiffon = new(255, 250, 205);

    /// <summary>Lilac (R:200 G:162 B:200)</summary>
    public static readonly Rgba Lilac = new(200, 162, 200);

    /// <summary>LightBlue (R:173 G:216 B:230)</summary>
    public static readonly Rgba LightBlue = new(173, 216, 230);

    /// <summary>LightCoral (R:240 G:128 B:128)</summary>
    public static readonly Rgba LightCoral = new(240, 128, 128);

    /// <summary>LightCyan (R:224 G:255 B:255)</summary>
    public static readonly Rgba LightCyan = new(224, 255, 255);

    /// <summary>LightGoldenrodYellow (R:250 G:250 B:210)</summary>
    public static readonly Rgba LightGoldenrodYellow = new(250, 250, 210);

    /// <summary>LightGray (R:211 G:211 B:211)</summary>
    public static readonly Rgba LightGray = new(211, 211, 211);

    /// <summary>LightGreen (R:144 G:238 B:144)</summary>
    public static readonly Rgba LightGreen = new(144, 238, 144);

    /// <summary>LightPink (R:255 G:182 B:193)</summary>
    public static readonly Rgba LightPink = new(255, 182, 193);

    /// <summary>LightSalmon (R:255 G:160 B:122)</summary>
    public static readonly Rgba LightSalmon = new(255, 160, 122);

    /// <summary>LightSeaGreen (R:32 G:178 B:170)</summary>
    public static readonly Rgba LightSeaGreen = new(32, 178, 170);

    /// <summary>LightSkyBlue (R:135 G:206 B:250)</summary>
    public static readonly Rgba LightSkyBlue = new(135, 206, 250);

    /// <summary>LightSlateGray (R:119 G:136 B:153)</summary>
    public static readonly Rgba LightSlateGray = new(119, 136, 153);

    /// <summary>LightSteelBlue (R:176 G:196 B:222)</summary>
    public static readonly Rgba LightSteelBlue = new(176, 196, 222);

    /// <summary>LightYellow (R:255 G:255 B:224)</summary>
    public static readonly Rgba LightYellow = new(255, 255, 224);

    /// <summary>Lime (R:0 G:255 B:0)</summary>
    public static readonly Rgba Lime = new(0, 255, 0);

    /// <summary>LimeGreen (R:50 G:205 B:50)</summary>
    public static readonly Rgba LimeGreen = new(50, 205, 50);

    /// <summary>Linen (R:250 G:240 B:230)</summary>
    public static readonly Rgba Linen = new(250, 240, 230);

    /// <summary>Magenta (R:255 G:0 B:255)</summary>
    public static readonly Rgba Magenta = new(255, 0, 255);

    /// <summary>Mahogany — a deep reddish-brown (R:192 G:64 B:0)</summary>
    public static readonly Rgba Mahogany = new(192, 64, 0);

    /// <summary>Maroon (R:128 G:0 B:0)</summary>
    public static readonly Rgba Maroon = new(128, 0, 0);

    /// <summary>Marigold (R:234 G:162 B:33)</summary>
    public static readonly Rgba Marigold = new(234, 162, 33);

    /// <summary>Mauve — a pale purple (R:224 G:176 B:255)</summary>
    public static readonly Rgba Mauve = new(224, 176, 255);

    /// <summary>MediumAquamarine (R:102 G:205 B:170)</summary>
    public static readonly Rgba MediumAquamarine = new(102, 205, 170);

    /// <summary>MediumBlue (R:0 G:0 B:205)</summary>
    public static readonly Rgba MediumBlue = new(0, 0, 205);

    /// <summary>MediumOrchid (R:186 G:85 B:211)</summary>
    public static readonly Rgba MediumOrchid = new(186, 85, 211);

    /// <summary>MediumPurple (R:147 G:112 B:219)</summary>
    public static readonly Rgba MediumPurple = new(147, 112, 219);

    /// <summary>MediumSeaGreen (R:60 G:179 B:113)</summary>
    public static readonly Rgba MediumSeaGreen = new(60, 179, 113);

    /// <summary>MediumSlateBlue (R:123 G:104 B:238)</summary>
    public static readonly Rgba MediumSlateBlue = new(123, 104, 238);

    /// <summary>MediumSpringGreen (R:0 G:250 B:154)</summary>
    public static readonly Rgba MediumSpringGreen = new(0, 250, 154);

    /// <summary>MediumTurquoise (R:72 G:209 B:204)</summary>
    public static readonly Rgba MediumTurquoise = new(72, 209, 204);

    /// <summary>MediumVioletRed (R:199 G:21 B:133)</summary>
    public static readonly Rgba MediumVioletRed = new(199, 21, 133);

    /// <summary>MidnightBlue (R:25 G:25 B:112)</summary>
    public static readonly Rgba MidnightBlue = new(25, 25, 112);

    /// <summary>Mint — a medium mint green, distinct from MintCream (R:62 G:180 B:137)</summary>
    public static readonly Rgba Mint = new(62, 180, 137);

    /// <summary>MintCream (R:245 G:255 B:250)</summary>
    public static readonly Rgba MintCream = new(245, 255, 250);

    /// <summary>MistyRose (R:255 G:228 B:225)</summary>
    public static readonly Rgba MistyRose = new(255, 228, 225);

    /// <summary>Moccasin (R:255 G:228 B:181)</summary>
    public static readonly Rgba Moccasin = new(255, 228, 181);

    /// <summary>Moss (R:138 G:154 B:91)</summary>
    public static readonly Rgba Moss = new(138, 154, 91);

    /// <summary>Mustard (R:255 G:219 B:88)</summary>
    public static readonly Rgba Mustard = new(255, 219, 88);

    /// <summary>NavajoWhite (R:255 G:222 B:173)</summary>
    public static readonly Rgba NavajoWhite = new(255, 222, 173);

    /// <summary>Navy (R:0 G:0 B:128)</summary>
    public static readonly Rgba Navy = new(0, 0, 128);

    /// <summary>Ochre — a brownish-yellow clay pigment (R:204 G:119 B:34)</summary>
    public static readonly Rgba Ochre = new(204, 119, 34);

    /// <summary>OldLace (R:253 G:245 B:230)</summary>
    public static readonly Rgba OldLace = new(253, 245, 230);

    /// <summary>Olive (R:128 G:128 B:0)</summary>
    public static readonly Rgba Olive = new(128, 128, 0);

    /// <summary>OliveDrab (R:107 G:142 B:35)</summary>
    public static readonly Rgba OliveDrab = new(107, 142, 35);

    /// <summary>Onyx — a near-black with a slight blue-grey cast (R:53 G:56 B:57)</summary>
    public static readonly Rgba Onyx = new(53, 56, 57);

    /// <summary>Orange (R:255 G:165 B:0)</summary>
    public static readonly Rgba Orange = new(255, 165, 0);

    /// <summary>OrangeRed (R:255 G:69 B:0)</summary>
    public static readonly Rgba OrangeRed = new(255, 69, 0);

    /// <summary>Orchid (R:218 G:112 B:214)</summary>
    public static readonly Rgba Orchid = new(218, 112, 214);

    /// <summary>PaleGoldenrod (R:238 G:232 B:170)</summary>
    public static readonly Rgba PaleGoldenrod = new(238, 232, 170);

    /// <summary>PaleGreen (R:152 G:251 B:152)</summary>
    public static readonly Rgba PaleGreen = new(152, 251, 152);

    /// <summary>PaleTurquoise (R:175 G:238 B:238)</summary>
    public static readonly Rgba PaleTurquoise = new(175, 238, 238);

    /// <summary>PaleVioletRed (R:219 G:112 B:147)</summary>
    public static readonly Rgba PaleVioletRed = new(219, 112, 147);

    /// <summary>PapayaWhip (R:255 G:239 B:213)</summary>
    public static readonly Rgba PapayaWhip = new(255, 239, 213);

    /// <summary>PeachPuff (R:255 G:218 B:185)</summary>
    public static readonly Rgba PeachPuff = new(255, 218, 185);

    /// <summary>Pear — a yellow-green (R:209 G:226 B:49)</summary>
    public static readonly Rgba Pear = new(209, 226, 49);

    /// <summary>Periwinkle — a light blue-violet (R:204 G:204 B:255)</summary>
    public static readonly Rgba Periwinkle = new(204, 204, 255);

    /// <summary>Peru (R:205 G:133 B:63)</summary>
    public static readonly Rgba Peru = new(205, 133, 63);

    /// <summary>Pink (R:255 G:192 B:203)</summary>
    public static readonly Rgba Pink = new(255, 192, 203);

    /// <summary>Plum (R:221 G:160 B:221)</summary>
    public static readonly Rgba Plum = new(221, 160, 221);

    /// <summary>PowderBlue (R:176 G:224 B:230)</summary>
    public static readonly Rgba PowderBlue = new(176, 224, 230);

    /// <summary>Puce — a brownish-pink (R:204 G:136 B:153)</summary>
    public static readonly Rgba Puce = new(204, 136, 153);

    /// <summary>Purple (R:128 G:0 B:128)</summary>
    public static readonly Rgba Purple = new(128, 0, 128);

    /// <summary>Red (R:255 G:0 B:0)</summary>
    public static readonly Rgba Red = new(255, 0, 0);

    /// <summary>RosyBrown (R:188 G:143 B:143)</summary>
    public static readonly Rgba RosyBrown = new(188, 143, 143);

    /// <summary>RoyalBlue (R:65 G:105 B:225)</summary>
    public static readonly Rgba RoyalBlue = new(65, 105, 225);

    /// <summary>Rust (R:183 G:65 B:14)</summary>
    public static readonly Rgba Rust = new(183, 65, 14);

    /// <summary>SaddleBrown (R:139 G:69 B:19)</summary>
    public static readonly Rgba SaddleBrown = new(139, 69, 19);

    /// <summary>Saffron — a golden-yellow spice colour (R:244 G:196 B:48)</summary>
    public static readonly Rgba Saffron = new(244, 196, 48);

    /// <summary>Sage — a grey-green (R:188 G:184 B:138)</summary>
    public static readonly Rgba Sage = new(188, 184, 138);

    /// <summary>Salmon (R:250 G:128 B:114)</summary>
    public static readonly Rgba Salmon = new(250, 128, 114);

    /// <summary>SandyBrown (R:244 G:164 B:96)</summary>
    public static readonly Rgba SandyBrown = new(244, 164, 96);

    /// <summary>Sapphire (R:15 G:82 B:186)</summary>
    public static readonly Rgba Sapphire = new(15, 82, 186);

    /// <summary>Scarlet (R:255 G:36 B:0)</summary>
    public static readonly Rgba Scarlet = new(255, 36, 0);

    /// <summary>SeaGreen (R:46 G:139 B:87)</summary>
    public static readonly Rgba SeaGreen = new(46, 139, 87);

    /// <summary>SeaShell (R:255 G:245 B:238)</summary>
    public static readonly Rgba SeaShell = new(255, 245, 238);

    /// <summary>Sepia (R:112 G:66 B:20)</summary>
    public static readonly Rgba Sepia = new(112, 66, 20);

    /// <summary>Sienna (R:160 G:82 B:45)</summary>
    public static readonly Rgba Sienna = new(160, 82, 45);

    /// <summary>Silver (R:192 G:192 B:192)</summary>
    public static readonly Rgba Silver = new(192, 192, 192);

    /// <summary>SkyBlue (R:135 G:206 B:235)</summary>
    public static readonly Rgba SkyBlue = new(135, 206, 235);

    /// <summary>SlateBlue (R:106 G:90 B:205)</summary>
    public static readonly Rgba SlateBlue = new(106, 90, 205);

    /// <summary>SlateGray (R:112 G:128 B:144)</summary>
    public static readonly Rgba SlateGray = new(112, 128, 144);

    /// <summary>Snow (R:255 G:250 B:250)</summary>
    public static readonly Rgba Snow = new(255, 250, 250);

    /// <summary>SpringGreen (R:0 G:255 B:127)</summary>
    public static readonly Rgba SpringGreen = new(0, 255, 127);

    /// <summary>SteelBlue (R:70 G:130 B:180)</summary>
    public static readonly Rgba SteelBlue = new(70, 130, 180);

    /// <summary>Tan (R:210 G:180 B:140)</summary>
    public static readonly Rgba Tan = new(210, 180, 140);

    /// <summary>Tangerine (R:242 G:133 B:0)</summary>
    public static readonly Rgba Tangerine = new(242, 133, 0);

    /// <summary>Taupe — a brownish-grey (R:72 G:60 B:50)</summary>
    public static readonly Rgba Taupe = new(72, 60, 50);

    /// <summary>Teal (R:0 G:128 B:128)</summary>
    public static readonly Rgba Teal = new(0, 128, 128);

    /// <summary>Thistle (R:216 G:191 B:216)</summary>
    public static readonly Rgba Thistle = new(216, 191, 216);

    /// <summary>Tomato (R:255 G:99 B:71)</summary>
    public static readonly Rgba Tomato = new(255, 99, 71);

    /// <summary>Turquoise (R:64 G:224 B:208)</summary>
    public static readonly Rgba Turquoise = new(64, 224, 208);

    /// <summary>Umber — a dark brown earth pigment (R:99 G:81 B:71)</summary>
    public static readonly Rgba Umber = new(99, 81, 71);

    /// <summary>Vermillion — a brilliant orange-red (R:227 G:66 B:52)</summary>
    public static readonly Rgba Vermillion = new(227, 66, 52);

    /// <summary>Violet (R:238 G:130 B:238)</summary>
    public static readonly Rgba Violet = new(238, 130, 238);

    /// <summary>Wheat (R:245 G:222 B:179)</summary>
    public static readonly Rgba Wheat = new(245, 222, 179);

    /// <summary>White (R:255 G:255 B:255)</summary>
    public static readonly Rgba White = new(255, 255, 255);

    /// <summary>WhiteSmoke (R:245 G:245 B:245)</summary>
    public static readonly Rgba WhiteSmoke = new(245, 245, 245);

    /// <summary>Wisteria — a soft blue-purple (R:201 G:160 B:220)</summary>
    public static readonly Rgba Wisteria = new(201, 160, 220);

    /// <summary>Yellow (R:255 G:255 B:0)</summary>
    public static readonly Rgba Yellow = new(255, 255, 0);

    /// <summary>YellowGreen (R:154 G:205 B:50)</summary>
    public static readonly Rgba YellowGreen = new(154, 205, 50);

    // ── Enum lookup ───────────────────────────────────────────────────────────

    // This array MUST stay in the same order as the Color enum.
    // Each index corresponds to the integer value of the matching Color member.
    private static readonly Rgba[] lookup =
    [
        Transparent, AliceBlue, Amber, Amethyst, AntiqueWhite, Apricot,
        Aqua, Aquamarine, Auburn, Azure, Beige, Bisque, Black,
        BlanchedAlmond, Blue, BlueViolet, Brown, Burgundy, BurlyWood,
        CadetBlue, Carmine, Cerulean, Champagne, Charcoal, Chartreuse,
        Chocolate, Cobalt, Copper, Coral, CornflowerBlue, Cornsilk, Cream,
        Crimson, Cyan, DarkBlue, DarkCyan, DarkGoldenrod, DarkGray,
        DarkGreen, DarkKhaki, DarkMagenta, DarkOliveGreen, DarkOrange,
        DarkOrchid, DarkRed, DarkSalmon, DarkSeaGreen, DarkSlateBlue,
        DarkSlateGray, DarkTurquoise, DarkViolet, DeepPink, DeepSkyBlue,
        DimGray, DodgerBlue, Ecru, Emerald, Fern, Firebrick, Flax,
        FloralWhite, ForestGreen, Fuchsia, Gainsboro, GhostWhite, Gold,
        Goldenrod, Gray, Green, GreenYellow, Honeydew, HotPink, IndianRed,
        Indigo, Ivory, Jade, Khaki, Lavender, LavenderBlush, LawnGreen,
        LemonChiffon, Lilac, LightBlue, LightCoral, LightCyan,
        LightGoldenrodYellow, LightGray, LightGreen, LightPink, LightSalmon,
        LightSeaGreen, LightSkyBlue, LightSlateGray, LightSteelBlue,
        LightYellow, Lime, LimeGreen, Linen, Magenta, Mahogany, Maroon,
        Marigold, Mauve, MediumAquamarine, MediumBlue, MediumOrchid,
        MediumPurple, MediumSeaGreen, MediumSlateBlue, MediumSpringGreen,
        MediumTurquoise, MediumVioletRed, MidnightBlue, Mint, MintCream,
        MistyRose, Moccasin, Moss, Mustard, NavajoWhite, Navy, Ochre,
        OldLace, Olive, OliveDrab, Onyx, Orange, OrangeRed, Orchid,
        PaleGoldenrod, PaleGreen, PaleTurquoise, PaleVioletRed, PapayaWhip,
        PeachPuff, Pear, Periwinkle, Peru, Pink, Plum, PowderBlue, Puce,
        Purple, Red, RosyBrown, RoyalBlue, Rust, SaddleBrown, Saffron, Sage,
        Salmon, SandyBrown, Sapphire, Scarlet, SeaGreen, SeaShell, Sepia,
        Sienna, Silver, SkyBlue, SlateBlue, SlateGray, Snow, SpringGreen,
        SteelBlue, Tan, Tangerine, Taupe, Teal, Thistle, Tomato, Turquoise,
        Umber, Vermillion, Violet, Wheat, White, WhiteSmoke, Wisteria,
        Yellow, YellowGreen,
    ];

    /// <summary>
    /// Returns the <see cref="Rgba"/> value for the given named <see cref="Color"/>.
    /// This is an O(1) array lookup.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="color"/> is not a defined enum member.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba Get(Color color)
    {
        int i = (int)color;
        if ((uint)i >= (uint)lookup.Length)
            ThrowOutOfRange(color);
        return lookup[i];
    }

    /// <summary>
    /// Finds the nearest named <see cref="Color"/> for an arbitrary <see cref="Rgba"/> value
    /// using squared Euclidean distance in RGB space. Alpha is ignored.
    /// </summary>
    public static Color FindNearest(Rgba target)
    {
        int best = 0;
        long bestDist = long.MaxValue;

        for (int i = 0; i < lookup.Length; i++)
        {
            int dr = target.R - lookup[i].R;
            int dg = target.G - lookup[i].G;
            int db = target.B - lookup[i].B;
            long dist = (long)dr * dr + (long)dg * dg + (long)db * db;

            if (dist >= bestDist) continue;
            bestDist = dist;
            best = i;
            if (dist == 0) break; // exact match
        }

        return (Color)best;
    }

    /// <summary>Total number of named colors.</summary>
    public static int Count => lookup.Length;

    /// <summary>Returns all named colors as a read-only span.</summary>
    public static ReadOnlySpan<Rgba> All => lookup;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOutOfRange(Color color)
        => throw new ArgumentOutOfRangeException(nameof(color), color,
            $"'{color}' is not a defined Color enum value.");
}

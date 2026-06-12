namespace SharpCraft.Extensions;

public static class StringExtensions
{
    public static string CapitalizeFirst(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}
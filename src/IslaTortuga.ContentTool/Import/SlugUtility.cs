using System.Text;
using System.Text.RegularExpressions;

namespace IslaTortuga.ContentTool.Import;

internal static class SlugUtility
{
    private static readonly Regex NonSlugCharacters = new("[^a-z0-9]+", RegexOptions.Compiled);

    public static string ToSlug(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = normalized
            .Replace("á", "a")
            .Replace("é", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ú", "u")
            .Replace("ñ", "n");

        normalized = NonSlugCharacters.Replace(normalized, "-").Trim('-');

        return string.IsNullOrWhiteSpace(normalized) ? "asset" : normalized;
    }

    public static string ToTextureKey(string value)
    {
        return $"tileset-{ToSlug(value)}";
    }
}

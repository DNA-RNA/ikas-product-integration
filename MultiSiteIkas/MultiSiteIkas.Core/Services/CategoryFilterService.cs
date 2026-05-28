using MultiSiteIkas.Core.Interfaces;

namespace MultiSiteIkas.Core.Services;

public sealed class CategoryFilterService : ICategoryFilterService
{
    public bool ShouldTransfer(string categoryPath, IReadOnlyList<string> filters)
    {
        if (string.IsNullOrWhiteSpace(categoryPath)) return false;
        if (filters is null || filters.Count == 0) return true; // filtre yoksa tümünü geçir

        var normalizedPath = NormalizePath(categoryPath);

        foreach (var rawFilter in filters)
        {
            var filter = NormalizePath(rawFilter);

            if (filter.EndsWith(" > *", StringComparison.Ordinal))
            {
                var prefix = filter[..^4];
                if (normalizedPath.StartsWith(prefix + " > ", StringComparison.OrdinalIgnoreCase))
                    return true;
                continue;
            }

            if (string.Equals(normalizedPath, filter, StringComparison.OrdinalIgnoreCase))
                return true;

            var segments = normalizedPath.Split(" > ", StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(s => string.Equals(s.Trim(), filter, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static string NormalizePath(string path) =>
        path.Trim()
            .Replace("  ", " ")
            .Replace(">", " > ")
            .Replace("  >", " >")
            .Replace(">  ", "> ")
            .Replace("  ", " ");
}

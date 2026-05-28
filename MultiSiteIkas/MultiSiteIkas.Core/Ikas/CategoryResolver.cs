using Microsoft.Extensions.Logging;
using MultiSiteIkas.Core.Interfaces;

namespace MultiSiteIkas.Core.Ikas;

public sealed class CategoryResolver : ICategoryResolver
{
    private readonly IIkasApiService _api;
    private readonly ILogger<CategoryResolver> _logger;
    private Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public CategoryResolver(IIkasApiService api, ILogger<CategoryResolver> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task RefreshAsync(IkasCredentials creds, CancellationToken ct = default)
    {
        try
        {
            var categories = await _api.ListCategoriesAsync(creds, ct);
            _cache = categories.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation("[{Store}] Loaded {Count} categories into cache", creds.StoreCode, _cache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Store}] Could not load categories — products will be sent without category", creds.StoreCode);
            _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public string? ResolveId(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return null;
        return _cache.TryGetValue(categoryName, out var id) ? id : null;
    }
}

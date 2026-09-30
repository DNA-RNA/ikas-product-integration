using Microsoft.Extensions.Logging;
using MultiSiteIkas.Core.Caching;
using MultiSiteIkas.Core.Interfaces;

namespace MultiSiteIkas.Core.Ikas;

public sealed class CategoryResolver(
    IIkasApiService api,
    ICacheService cache,
    ILogger<CategoryResolver> logger) : ICategoryResolver
{
    // Scoped servis: her job için yeni instance oluşturulur.
    // Ama ICacheService Singleton — veri cache'de job'lar arası yaşar.
    // Bu field sadece bu job'un hangi mağaza cache'ini okuyacağını tutar.
    private string? _storeKey;

    public async Task RefreshAsync(IkasCredentials creds, CancellationToken ct = default)
    {
        _storeKey = creds.ApiKey;
        var cacheKey = $"ikas:categories:{creds.ApiKey}";

        // Aynı mağaza için birden fazla tedarikçi job'u çalışsa bile
        // kategori listesi tek seferlik çekilir
        var existing = cache.Get<Dictionary<string, string>>(cacheKey);
        if (existing is not null)
        {
            logger.LogInformation("[{Store}] Kategoriler cache'den alındı ({Count} adet)",
                creds.StoreCode, existing.Count);
            return;
        }

        try
        {
            var categories = await api.ListCategoriesAsync(creds, ct);
            var dict = categories.ToDictionary(
                c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

            cache.Set(cacheKey, dict, TimeSpan.FromHours(1));

            logger.LogInformation("[{Store}] {Count} kategori API'den yüklendi ve cache'lendi",
                creds.StoreCode, dict.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{Store}] Kategori listesi alınamadı — ürünler kategorisiz gönderilecek",
                creds.StoreCode);
            // Hata durumunda 5 dk boş cache — sürekli retry'ı önler
            cache.Set(cacheKey, new Dictionary<string, string>(), TimeSpan.FromMinutes(5));
        }
    }

    public string? ResolveId(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName) || _storeKey is null) return null;
        var dict = cache.Get<Dictionary<string, string>>($"ikas:categories:{_storeKey}");
        return dict?.TryGetValue(categoryName, out var id) == true ? id : null;
    }
}

namespace MultiSiteIkas.Core.Caching;

/// <summary>
/// Uygulama geneli cache soyutlaması.
/// Implementasyon değiştiğinde (IMemoryCache → Redis) bu interface'i kullanan
/// hiçbir kod değişmez — sadece DI kaydı güncellenir.
/// </summary>
public interface ICacheService
{
    /// <summary>Cache'den okur. Yoksa default(T) döner.</summary>
    T? Get<T>(string key);

    /// <summary>Cache'e yazar. TTL sonunda otomatik silinir.</summary>
    void Set<T>(string key, T value, TimeSpan ttl);

    /// <summary>Cache'den siler (örn: kategori eklendiğinde invalidate için).</summary>
    void Remove(string key);

    /// <summary>
    /// Cache'de varsa döner, yoksa factory'yi çalıştırır, sonucu cache'e yazar.
    /// En çok kullanılan pattern: "varsa al, yoksa hesapla ve sakla"
    /// </summary>
    async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
        where T : class
    {
        var cached = Get<T>(key);
        if (cached is not null) return cached;

        var value = await factory();
        Set(key, value, ttl);
        return value;
    }
}

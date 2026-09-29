using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MultiSiteIkas.Core.Caching;

/// <summary>
/// IMemoryCache tabanlı implementasyon — tek process, RAM içi.
///
/// Redis'e geçmek istersen:
///   1. RedisDistributedCacheService : ICacheService yaz
///   2. Program.cs'de AddStackExchangeRedisCache + kayıt değiştir
///   3. Bu sınıf ve onu kullanan kodlar değişmez
/// </summary>
public sealed class MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger) : ICacheService
{
    public T? Get<T>(string key)
    {
        if (cache.TryGetValue(key, out T? value))
        {
            logger.LogDebug("[Cache] HIT — {Key}", key);
            return value;
        }

        logger.LogDebug("[Cache] MISS — {Key}", key);
        return default;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            // Bellek baskısı altında düşük öncelikli cache'ler temizlenir
            Priority = CacheItemPriority.Normal
        };

        cache.Set(key, value, options);
        logger.LogDebug("[Cache] SET — {Key} (TTL: {TTL})", key, ttl);
    }

    public void Remove(string key)
    {
        cache.Remove(key);
        logger.LogDebug("[Cache] REMOVE — {Key}", key);
    }
}

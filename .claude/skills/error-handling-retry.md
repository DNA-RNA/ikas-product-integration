# Error Handling, Retry, Circuit Breaker — Polly

## NuGet
```xml
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.*" />
<PackageReference Include="Polly" Version="8.*" />
<PackageReference Include="Polly.Extensions.Http" Version="3.*" />
```

## Custom Exception'lar (Core/Exceptions/)

```csharp
public sealed class IkasApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string StoreCode { get; }

    public IkasApiException(string storeCode, HttpStatusCode statusCode, string message, Exception? inner = null)
        : base($"[{storeCode}] İkas API error: {message}", inner)
    {
        StoreCode = storeCode;
        StatusCode = statusCode;
    }
}

public sealed class XmlValidationException : Exception
{
    public XmlValidationException(string message) : base(message) { }
}

public sealed class SiteMappingNotFoundException : Exception
{
    public SiteMappingNotFoundException(long id) : base($"SiteMapping {id} not found") { }
}

public sealed class CategoryResolutionException : Exception
{
    public string CategoryName { get; }
    public long SiteMappingId { get; }

    public CategoryResolutionException(long siteMappingId, string categoryName)
        : base($"Cannot resolve category '{categoryName}' for site mapping {siteMappingId}")
    {
        SiteMappingId = siteMappingId;
        CategoryName = categoryName;
    }
}
```

## HTTP Client Registration

```csharp
builder.Services.AddHttpClient<IIkasApiService, IkasApiService>(client =>
{
    client.BaseAddress = new Uri("https://api.myikas.com");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

// XML indirme için ayrı client
builder.Services.AddHttpClient("xml-downloader", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddPolicyHandler(GetXmlRetryPolicy());
```

## Retry Policy (HTTP)

```csharp
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()                           // 5xx, 408
        .OrResult(r => (int)r.StatusCode == 429)              // Too Many Requests
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: (attempt, response, _) =>
            {
                // 429 ise Retry-After header'a uy
                if (response.Result?.Headers.RetryAfter is { } ra)
                {
                    if (ra.Delta is { } delta) return delta;
                    if (ra.Date is { } date)
                    {
                        var wait = date - DateTimeOffset.UtcNow;
                        return wait > TimeSpan.Zero ? wait : TimeSpan.FromSeconds(5);
                    }
                }
                // Exponential backoff: 2s, 4s, 8s
                return TimeSpan.FromSeconds(Math.Pow(2, attempt));
            },
            onRetryAsync: (outcome, delay, attempt, _) =>
            {
                Log.Warning(
                    "HTTP retry {Attempt}/3 after {Delay}s — status: {Status} url: {Url}",
                    attempt, delay.TotalSeconds,
                    outcome.Result?.StatusCode,
                    outcome.Result?.RequestMessage?.RequestUri);
                return Task.CompletedTask;
            });
```

## Circuit Breaker

```csharp
static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromMinutes(1),
            onBreak: (outcome, breakDelay) =>
                Log.Error(
                    "Circuit broken for {Delay}s due to {Status}",
                    breakDelay.TotalSeconds, outcome.Result?.StatusCode),
            onReset: () => Log.Information("Circuit closed (recovered)"));
```

## XML Pull Retry

```csharp
static IAsyncPolicy<HttpResponseMessage> GetXmlRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(60 * Math.Pow(3, attempt - 1)),
            // 60s, 180s, 540s — XML çekme yavaş olabilir
            onRetryAsync: (outcome, delay, attempt, _) =>
            {
                Log.Warning("XML pull retry {Attempt}/3 after {Delay}s", attempt, delay.TotalSeconds);
                return Task.CompletedTask;
            });
```

## Job Entry Point — Exception Handling

```csharp
public async Task ExecuteAsync(long siteMappingId, CancellationToken ct)
{
    try
    {
        await _transferService.RunTransferAsync(siteMappingId, ct);
    }
    catch (SiteMappingNotFoundException ex)
    {
        _logger.LogError(ex, "Site mapping not found, skipping");
        // Hangfire'a hata olarak gösterilsin
        throw;
    }
    catch (IkasApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
    {
        _logger.LogCritical(ex, "Auth failed for {Store} — credentials invalid, manual intervention needed",
            ex.StoreCode);
        // Hangfire retry yapsa da çözmez ama retry attribute'ü ile sınırlı kalır
        throw;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        _logger.LogInformation("Job cancelled for SiteMapping {Id}", siteMappingId);
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unhandled error in transfer job for SiteMapping {Id}", siteMappingId);
        throw;
    }
}
```

## Service İçinde — Ürün Bazlı Hata Yönetimi

`TransferService` her ürünü ayrı try-catch ile sarmalar, bir ürünün hatası diğerlerini etkilemez:

```csharp
public async Task<TransferResult> RunTransferAsync(long siteMappingId, CancellationToken ct)
{
    var mapping = await _siteMappings.GetByIdAsync(siteMappingId, ct)
        ?? throw new SiteMappingNotFoundException(siteMappingId);

    if (!mapping.IsActive)
    {
        _logger.LogInformation("SiteMapping {Id} is inactive, skipping", siteMappingId);
        return TransferResult.Empty(siteMappingId);
    }

    var logEntry = await _logs.StartAsync(mapping, ct);

    var products = await _products.GetActiveProductsAsync(mapping.XmlSourceId, ct);
    var filtered = products.Where(p => _filter.ShouldTransfer(p.CategoryPath, mapping.CategoryFilters)).ToList();

    var (success, errors, skipped) = (0, 0, 0);
    var errorList = new ConcurrentBag<TransferError>();

    using var semaphore = new SemaphoreSlim(5);
    var tasks = filtered.Select(async product =>
    {
        await semaphore.WaitAsync(ct);
        try
        {
            await TransferOneAsync(product, mapping, ct);
            Interlocked.Increment(ref success);
        }
        catch (IkasApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Kritik — tüm job'ı abort
            throw;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref errors);
            errorList.Add(new TransferError(product.Sku, ex.Message));
            _logger.LogError(ex, "Failed transferring {Sku} to {Store}", product.Sku, mapping.StoreCode);
            await _transfers.RecordFailureAsync(product.Id, mapping, ex.Message, ct);
        }
        finally
        {
            semaphore.Release();
        }
    });

    await Task.WhenAll(tasks);

    await _logs.CompleteAsync(logEntry, products.Count, filtered.Count, success, errors, skipped, errorList, ct);

    return new TransferResult(logEntry.Id, products.Count, filtered.Count, success, errors, skipped,
        DateTime.UtcNow - logEntry.StartDate, errorList.ToList());
}
```

## Logging Levels

| Seviye | Ne zaman |
|--------|----------|
| `Information` | Job başladı/bitti, ürün başarıyla transfer edildi |
| `Warning` | Retry yapıldı, mapping bulunamadı (skip), ürün eksik veri |
| `Error` | Tek ürün transfer hatası, validation hatası |
| `Critical` | Auth hatası, DB connection kaybı, tüm job abort |

## Hangfire AutomaticRetry vs Polly

İki ayrı retry katmanı:
- **Polly**: Tek bir HTTP isteği seviyesinde (örn. 503 dönerse 3 saniye sonra dene).
- **Hangfire AutomaticRetry**: Tüm job seviyesinde (örn. job exception fırlattı, 1 dakika sonra job'ı baştan başlat).

İkisi farklı amaçlar:
- Polly: Geçici network/server hataları için fast retry.
- Hangfire: Beklenmedik exception sonrası slow retry, manuel müdahaleye fırsat verir.

## Production Monitoring

- **Serilog → Seq**: structured log query yeteneği.
- **Hangfire Dashboard**: job status, retry, manuel tetikleme.
- **Sync Logs Table**: business-level audit trail.
- **Health Check Endpoint**: `/health` → DB ping + her hedef İkas API ping.
- (Faz 2) **Alerting**: Hata oranı %5'i aşınca Slack/email bildirimi.

## Yasaklar

- `catch (Exception) { }` boş swallow — daima en azından log.
- `catch (Exception ex) { return false; }` — sahte success, retry mekanizmasını bozar.
- Custom exception'larda yapıcı parametresi sırasını rastgele değiştirme — tutarlı tut.
- Production secret'larını exception mesajına koyma (API key vs.).
- `Console.WriteLine` ile log — daima Serilog.

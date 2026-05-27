# Transfer Flow — TransferService Akış Diyagramı

Bu skill `TransferService`'in tam akışını ve edge case'lerini anlatır. `transfer-orkestratoru` agent'ı bu dosyaya başvurur.

## Üst Düzey Akış

```
RunTransferAsync(siteMappingId)
   │
   ├── 1. SiteMapping al
   │     ├── yok → SiteMappingNotFoundException (Hangfire retry'a alır)
   │     └── is_active=false → erken çık, "skipped" log
   │
   ├── 2. transfer_logs tablosuna yeni kayıt (status=InProgress, start_date=now)
   │
   ├── 3. products tablosundan aktif ürünleri çek
   │     (xml_source_id eşleşen, is_active=true)
   │
   ├── 4. CategoryFilterService ile filtrele
   │     filtered = products.Where(p => filter.ShouldTransfer(p.CategoryPath, mapping.CategoryFilters))
   │
   ├── 5. Paralel transfer (SemaphoreSlim(5)):
   │     foreach filtered:
   │        TransferOneAsync(product, mapping, ct)
   │        ├── 5a. PricingService.CalculateSitePrice
   │        ├── 5b. IkasFieldMapper.MapToIkasProduct
   │        ├── 5c. product_transfers'tan existing kaydı çek
   │        │      ├── varsa: existing.ikas_product_id → input.Id (UPDATE path)
   │        │      └── yoksa: input.Id = null, ama önce SKU ile İkas API'de listProduct (safety)
   │        ├── 5d. IkasApiService.SaveProductAsync
   │        ├── 5e. product_transfers'a upsert (success: ikas_product_id kaydet)
   │        └── 5f. hata → product_transfers.error_message + sync_log
   │
   ├── 6. transfer_logs güncelle (status=Success/Failed, counts, duration)
   │
   └── 7. TransferResult döndür
```

## Edge Case'ler

### Mapping problemleri
| Durum | Davranış |
|-------|----------|
| `category_filters` boş | Hiçbir ürün filter'dan geçmez, success_count=0, hata değil |
| `category_mappings` yok | Fallback: kategori path'inin son segmentini kullan |
| `brand_mappings` yok | Master'daki brand ismini olduğu gibi gönder |
| `price_margin_percentage = 0` | Sadece `additional_price` eklenir, hata değil |

### Ürün veri problemleri
| Durum | Davranış |
|-------|----------|
| sale_price = 0 ve price = 0 | Skip + log "no valid price" |
| stock < 0 | 0 olarak normalize |
| name çok uzun (>1000) | Truncate |
| name boş | Skip + log (XML parser zaten yakalamalı) |
| images boş | Resimsiz transfer |
| brand boş | Brand'siz transfer |

### İkas API hataları
| Durum | Davranış |
|-------|----------|
| 401 Unauthorized | TÜM job abort, status=Failed, kritik log |
| 403 Forbidden | TÜM job abort (scope eksik) |
| 429 Too Many Requests | Polly retry (Retry-After header) |
| 5xx | Polly retry (3 deneme exponential) |
| 400 Validation (örn. duplicate slug) | Tek ürün skip, error_count++ |
| Timeout | Polly retry |

### Concurrency
- Aynı SiteMapping iki kere paralel çalışamaz (`DisableConcurrentExecution`).
- Aynı SiteMapping içinde max 5 paralel ürün transferi (SemaphoreSlim).
- Farklı SiteMapping'ler birbirinden bağımsız (recinem.com ve boncukpasaji.com aynı anda transfer olabilir).

### Cancellation
- `ct.IsCancellationRequested` her await öncesi kontrol edilir (CancellationToken'ı geç).
- Cancel olursa: işlenmemiş ürünler "Pending" kalır, log status=Cancelled.
- Bir sonraki sync'te baştan başlanır (idempotent, sorun değil).

### "Last seen" Mantığı
Master'da bir ürün silinirse XML'de görünmez. Bunu yakalamak için:
```sql
-- 7 günden eski "son görülme" tarihi olan ürünleri inaktif yap
UPDATE products
SET is_active = 0, updated_date = GETUTCDATE()
WHERE last_seen_date < DATEADD(DAY, -7, GETUTCDATE())
  AND is_active = 1;
```

Bu bir cleanup job'ı olarak çalıştırılır (haftalık). Transfer job'ı `is_active=true` olanları çeker.

İkas tarafında ne yapılır? Bu ürünlerin `product_transfers` kaydı varsa, hedef İkas'ta da pasifleştir (variant.isActive=false). Cleanup job bunu da yapmalı.

## TransferOneAsync Pseudo-code

```csharp
private async Task TransferOneAsync(Product product, SiteMapping mapping, CancellationToken ct)
{
    // 1) Mevcut transfer kaydı var mı?
    var existing = await _transfers.FindAsync(product.Id, mapping.TargetCompanyId, ct);
    string? existingIkasId = existing?.IkasProductId;

    // 2) Yoksa İkas'ta SKU ile ara (master kopya senaryosu için)
    if (existingIkasId is null)
    {
        var creds = new IkasCredentials(mapping.IkasApiKey, mapping.IkasApiSecret, mapping.TargetCompany.Name);
        var foundInIkas = await _ikasApi.FindBySkuAsync(creds, product.Sku, ct);
        existingIkasId = foundInIkas?.Id;
    }

    // 3) Map et
    var input = _mapper.MapToIkasProduct(product, mapping);
    input.Id = existingIkasId; // null ise create, doluysa update

    // 4) İkas'a gönder
    var creds2 = new IkasCredentials(mapping.IkasApiKey, mapping.IkasApiSecret, mapping.TargetCompany.Name);
    var saved = await _ikasApi.SaveProductAsync(creds2, input, ct);

    // 5) product_transfers tablosuna upsert
    await _transfers.UpsertAsync(new ProductTransfer
    {
        SourceProductId = product.Id,
        TargetCompanyId = mapping.TargetCompanyId,
        SiteMappingId = mapping.Id,
        IkasProductId = saved.Id,
        IkasVariantId = saved.Variants.FirstOrDefault()?.Id,
        TargetSku = product.Sku,
        TransferredPrice = input.Variants[0].Prices[0].SellPrice,
        TransferredCategory = input.ProductCategories.FirstOrDefault()?.CategoryId,
        TransferStatus = TransferStatus.Success,
        ErrorMessage = null,
        FirstTransferDate = existing?.FirstTransferDate ?? DateTime.UtcNow,
        LastTransferDate = DateTime.UtcNow
    }, ct);
}
```

## "RecordFailureAsync" Method

```csharp
public async Task RecordFailureAsync(long productId, SiteMapping mapping, string errorMessage, CancellationToken ct)
{
    var existing = await _transfers.FindAsync(productId, mapping.TargetCompanyId, ct);
    if (existing is null)
    {
        await _transfers.AddAsync(new ProductTransfer
        {
            SourceProductId = productId,
            TargetCompanyId = mapping.TargetCompanyId,
            SiteMappingId = mapping.Id,
            TransferStatus = TransferStatus.Failed,
            ErrorMessage = errorMessage,
            RetryCount = 0,
            CreatedDate = DateTime.UtcNow
        }, ct);
    }
    else
    {
        existing.TransferStatus = TransferStatus.Failed;
        existing.ErrorMessage = errorMessage;
        existing.RetryCount++;
        existing.LastTransferDate = DateTime.UtcNow;
        await _transfers.UpdateAsync(existing, ct);
    }
}
```

## Performans Beklentileri

10K ürün × 4 hedef mağaza, 5 paralel, ortalama 500ms/ürün (İkas API latency):
- 1 mağaza için ~2000 ürün filtrelendi varsayalım: 2000 / 5 × 500ms = 200 saniye ≈ 3.5 dakika
- 4 mağaza sıralı çalışırsa: ~15 dakika
- Paralel çalışırsa (farklı job'lar): ~4 dakika

Hangfire schedule'da bu sürelerle çakışma olmamasına dikkat et.

## Monitoring

Her transfer job sonunda:
- `transfer_logs` kaydı tamamen dolu
- Serilog'a structured log: counts, duration, store
- Hangfire dashboard'da job success/failure görünür
- Hata oranı %5'i geçerse alert (Faz 2)

## Test Senaryoları

```csharp
[Fact]
public async Task RunTransferAsync_WhenMappingNotFound_Throws()

[Fact]
public async Task RunTransferAsync_WhenInactiveMapping_ReturnsEmptyResult()

[Fact]
public async Task RunTransferAsync_WhenNoProductsMatchFilter_ReturnsZeroSuccess()

[Fact]
public async Task RunTransferAsync_WithFiveProducts_AllSuccess_RecordsTransfers()

[Fact]
public async Task RunTransferAsync_WhenOneProductFails_OthersStillSucceed()

[Fact]
public async Task RunTransferAsync_OnAuthError_AbortsEntireJob()

[Fact]
public async Task RunTransferAsync_OnCancellation_GracefulShutdown()
```

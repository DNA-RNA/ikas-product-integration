using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiSiteIkas.Core.Exceptions;
using MultiSiteIkas.Core.Ikas;
using MultiSiteIkas.Core.Interfaces;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Core.Transfer;

public sealed class TransferService : ITransferService
{
    private readonly ISiteMappingRepository _mappings;
    private readonly ICompanyRepository _companies;
    private readonly IProductRepository _products;
    private readonly IProductTransferRepository _transfers;
    private readonly ITransferLogRepository _logs;
    private readonly ICategoryFilterService _filter;
    private readonly IIkasApiService _ikas;
    private readonly IIkasFieldMapper _mapper;
    private readonly ILogger<TransferService> _logger;

    public TransferService(
        ISiteMappingRepository mappings,
        ICompanyRepository companies,
        IProductRepository products,
        IProductTransferRepository transfers,
        ITransferLogRepository logs,
        ICategoryFilterService filter,
        IIkasApiService ikas,
        IIkasFieldMapper mapper,
        ILogger<TransferService> logger)
    {
        _mappings = mappings;
        _companies = companies;
        _products = products;
        _transfers = transfers;
        _logs = logs;
        _filter = filter;
        _ikas = ikas;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<TransferResult> RunTransferAsync(long siteMappingId, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        var mapping = await _mappings.GetByIdAsync(siteMappingId, ct)
            ?? throw new InvalidOperationException($"SiteMapping {siteMappingId} not found");

        if (!mapping.IsActive)
        {
            _logger.LogInformation("SiteMapping {Id} is inactive, skipping", siteMappingId);
            return new TransferResult { SiteMappingId = siteMappingId, StoreCode = "inactive" };
        }

        var company = await _companies.GetByIdAsync(mapping.TargetCompanyId, ct)
            ?? throw new InvalidOperationException($"Company {mapping.TargetCompanyId} not found");

        if (string.IsNullOrEmpty(company.IkasApiKey) || string.IsNullOrEmpty(company.IkasApiSecret))
            throw new InvalidOperationException($"Company {company.Name} has no İkas API credentials");

        var creds = new IkasCredentials(company.IkasApiKey, company.IkasApiSecret, company.Name);

        var logId = await _logs.CreateAsync(new TransferLog
        {
            XmlSourceId = mapping.XmlSourceId,
            SiteMappingId = mapping.Id,
            TargetCompanyId = mapping.TargetCompanyId,
            JobType = "Transfer",
            StartDate = start,
            Status = 0
        }, ct);

        int successCount = 0, failedCount = 0, skippedCount = 0;

        try
        {
            var allProducts = (await _products.GetByXmlSourceIdAsync(mapping.XmlSourceId, ct)).ToList();

            var categoryFilters = ParseFilters(mapping.CategoryFilters);
            var categoryMappings = ParseJson<Dictionary<string, string>>(mapping.CategoryMappings);

            var filtered = allProducts
                .Where(p => _filter.ShouldTransfer(p.CategoryPath, categoryFilters))
                .ToList();

            _logger.LogInformation("[{Store}] {Filtered}/{Total} ürün kategori filtresini geçti",
                company.Name, filtered.Count, allProducts.Count);

            if (filtered.Count == 0)
            {
                await FinalizeLogAsync(logId, 0, 0, 0, 0, allProducts.Count, start, null, ct);
                return new TransferResult
                {
                    SiteMappingId = siteMappingId,
                    StoreCode = company.Name,
                    TotalProductsFetched = allProducts.Count,
                    FilteredCount = 0,
                    Duration = DateTime.UtcNow - start
                };
            }

            // Aynı isme sahip DB satırlarını grupla → 1 İkas ürünü = 1 grup
            var productGroups = filtered
                .GroupBy(p => p.Name.Trim())
                .Select(g => g.ToList())
                .ToList();

            _logger.LogInformation("[{Store}] {Groups} ürün grubu transfer edilecek ({Variants} varyant toplam)",
                company.Name, productGroups.Count, filtered.Count);

            using var semaphore = new SemaphoreSlim(5, 5);
            var tasks = productGroups.Select(group => Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    await TransferGroupAsync(group, mapping, creds, categoryMappings, ct);
                    Interlocked.Add(ref successCount, group.Count);
                }
                catch (IkasApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized
                    or System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogCritical(ex, "[{Store}] Auth hatası — job durduruluyor", company.Name);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Store}] Ürün grubu transfer hatası: {Name}", company.Name, group[0].Name);
                    Interlocked.Add(ref failedCount, group.Count);

                    foreach (var p in group)
                        await RecordFailureAsync(p.Id, mapping, ex.Message, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct)).ToList();

            await Task.WhenAll(tasks);

            await FinalizeLogAsync(logId, successCount, failedCount, skippedCount,
                filtered.Count, allProducts.Count, start, null, ct);

            return new TransferResult
            {
                SiteMappingId = siteMappingId,
                StoreCode = company.Name,
                TotalProductsFetched = allProducts.Count,
                FilteredCount = filtered.Count,
                SuccessCount = successCount,
                FailedCount = failedCount,
                SkippedCount = skippedCount,
                Duration = DateTime.UtcNow - start
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SiteMapping {Id}] Transfer job başarısız", siteMappingId);
            await _logs.UpdateStatusAsync(logId, 2, (long)(DateTime.UtcNow - start).TotalMilliseconds, ex.Message, ct);
            throw;
        }
    }

    private async Task TransferGroupAsync(
        IReadOnlyList<Product> group,
        SiteMapping mapping,
        IkasCredentials creds,
        Dictionary<string, string>? categoryMappings,
        CancellationToken ct)
    {
        // Mevcut transfer kayıtlarını bul
        var existingTransfers = new List<ProductTransfer?>();
        string? existingIkasId = null;

        foreach (var p in group)
        {
            var existing = await _transfers.GetBySourceAndTargetAsync(p.Id, mapping.TargetCompanyId, ct);
            existingTransfers.Add(existing);
            if (existing?.IkasProductId != null && existingIkasId == null)
                existingIkasId = existing.IkasProductId;
        }

        // DB'de kayıt yoksa İkas'ta SKU bazlı ara
        if (existingIkasId == null)
        {
            var withSku = group.FirstOrDefault(p => !string.IsNullOrEmpty(p.Sku));
            if (withSku != null)
            {
                var found = await _ikas.FindBySkuAsync(creds, withSku.Sku, ct);
                existingIkasId = found?.Id;
            }
        }

        var resolvedCategoryName = ResolveCategoryName(group[0].CategoryPath, categoryMappings);
        var input = _mapper.Map(group, mapping, resolvedCategoryName);
        input.Id = existingIkasId;

        // Mevcut variant ID'lerini input'a aktar (update için)
        for (int i = 0; i < input.Variants.Length && i < existingTransfers.Count; i++)
            input.Variants[i].VariantId = existingTransfers[i]?.IkasVariantId;

        var saved = await _ikas.SaveProductAsync(creds, input, ct);
        var now = DateTime.UtcNow;

        for (int i = 0; i < group.Count; i++)
        {
            var product = group[i];
            var existing = existingTransfers[i];
            var savedVariant = i < saved.Variants.Count ? saved.Variants[i] : saved.Variants.FirstOrDefault();

            if (existing == null)
            {
                await _transfers.CreateAsync(new ProductTransfer
                {
                    SourceProductId   = product.Id,
                    TargetCompanyId   = mapping.TargetCompanyId,
                    SiteMappingId     = mapping.Id,
                    IkasProductId     = saved.Id,
                    IkasVariantId     = savedVariant?.Id,
                    TargetSku         = product.Sku,
                    TransferredPrice  = i < input.Variants.Length ? input.Variants[i].SellPrice : 0,
                    TransferredCategory = resolvedCategoryName,
                    TransferStatus    = 1,
                    FirstTransferDate = now,
                    LastTransferDate  = now
                }, ct);
            }
            else
            {
                existing.IkasProductId      = saved.Id;
                existing.IkasVariantId      = savedVariant?.Id;
                existing.TransferredPrice   = i < input.Variants.Length ? input.Variants[i].SellPrice : 0;
                existing.TransferredCategory = resolvedCategoryName;
                existing.TransferStatus     = 1;
                existing.ErrorMessage       = null;
                existing.LastTransferDate   = now;
                await _transfers.UpdateAsync(existing, ct);
            }
        }

        _logger.LogInformation("[{Store}] '{Name}' → İkas {IkasId} ({Count} varyant)",
            creds.StoreCode, group[0].Name, saved.Id, group.Count);
    }

    private async Task RecordFailureAsync(long productId, SiteMapping mapping, string errorMessage, CancellationToken ct)
    {
        var existing = await _transfers.GetBySourceAndTargetAsync(productId, mapping.TargetCompanyId, ct);
        if (existing == null)
        {
            await _transfers.CreateAsync(new ProductTransfer
            {
                SourceProductId = productId,
                TargetCompanyId = mapping.TargetCompanyId,
                SiteMappingId   = mapping.Id,
                TransferStatus  = 2,
                ErrorMessage    = errorMessage,
                RetryCount      = 0
            }, ct);
        }
        else
        {
            existing.TransferStatus = 2;
            existing.ErrorMessage   = errorMessage;
            existing.RetryCount++;
            existing.LastTransferDate = DateTime.UtcNow;
            await _transfers.UpdateAsync(existing, ct);
        }
    }

    private async Task FinalizeLogAsync(
        long logId, int success, int failed, int skipped, int filtered, int total,
        DateTime start, string? error, CancellationToken ct)
    {
        var end = DateTime.UtcNow;
        await _logs.UpdateAsync(new TransferLog
        {
            Id                     = logId,
            EndDate                = end,
            DurationMs             = (long)(end - start).TotalMilliseconds,
            TotalProductsProcessed = filtered,
            SuccessCount           = success,
            FailedCount            = failed,
            SkippedCount           = skipped,
            Status                 = (byte)(failed > 0 ? 2 : 1),
            ErrorMessage           = error
        }, ct);
    }

    private static IReadOnlyList<string> ParseFilters(string? json)
    {
        if (string.IsNullOrEmpty(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static T? ParseJson<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return null; }
    }

    private static string ResolveCategoryName(string categoryPath, Dictionary<string, string>? mappings)
    {
        if (mappings != null && mappings.TryGetValue(categoryPath, out var mapped)) return mapped;
        return categoryPath.Split('>').Last().Trim();
    }
}

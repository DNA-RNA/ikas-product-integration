using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiSiteIkas.Core.Interfaces;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Core.Xml;

public sealed class XmlPullService : IXmlPullService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IXmlParsingService _parser;
    private readonly IXmlSourceRepository _xmlSources;
    private readonly IProductRepository _products;
    private readonly ILogger<XmlPullService> _logger;

    public XmlPullService(
        IHttpClientFactory httpFactory,
        IXmlParsingService parser,
        IXmlSourceRepository xmlSources,
        IProductRepository products,
        ILogger<XmlPullService> logger)
    {
        _httpFactory = httpFactory;
        _parser = parser;
        _xmlSources = xmlSources;
        _products = products;
        _logger = logger;
    }

    public async Task<XmlPullResult> PullAsync(long xmlSourceId, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        var source = await _xmlSources.GetByIdAsync(xmlSourceId, ct)
            ?? throw new InvalidOperationException($"XmlSource {xmlSourceId} bulunamadı");

        _logger.LogInformation("XML pull başlatıldı: {Id} ({Name})", source.Id, source.Name);

        try
        {
            var http = _httpFactory.CreateClient("XmlDownloader");
            using var response = await http.GetAsync(source.XmlUrl, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var parsed = await _parser.ParseAsync(stream, ct);

            _logger.LogInformation("{Count} ürün parse edildi", parsed.Count);

            var entities = parsed.Select(p => new Product
            {
                CompanyId    = source.SourceCompanyId,
                XmlSourceId  = source.Id,
                ExternalId   = p.ExternalId,
                Sku          = p.Sku,
                Barcode      = p.Barcode,
                Name         = p.Name,
                Description  = p.Description,
                CategoryPath = p.CategoryPath,
                Brand        = p.Brand,
                OriginalPrice = p.OriginalPrice,
                SalePrice    = p.SalePrice,
                DiscountPrice = p.DiscountPrice,
                Currency     = p.Currency,
                StockQuantity = p.StockQuantity,
                Weight       = p.Weight,
                ImagesJson   = p.Images.Count > 0 ? JsonSerializer.Serialize(p.Images) : null,
                AttributesJson = p.Attributes.Count > 0 ? JsonSerializer.Serialize(p.Attributes) : null,
                IsActive     = true,
                IsDeleted    = false
            }).ToList();

            var upserted = await _products.BulkUpsertAsync(entities, ct);

            await _xmlSources.UpdateSyncStatusAsync(
                source.Id, "Success",
                DateTime.UtcNow.AddHours(source.SyncFrequencyHours), ct);

            var duration = DateTime.UtcNow - start;
            _logger.LogInformation("XML pull tamamlandı: {Upserted} upsert, {Duration:g}", upserted, duration);

            return new XmlPullResult
            {
                XmlSourceId   = xmlSourceId,
                ProductsFound = parsed.Count,
                Upserted      = upserted,
                Duration      = duration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XML pull başarısız: source {Id}", xmlSourceId);
            await _xmlSources.UpdateSyncStatusAsync(source.Id, "Failed", DateTime.UtcNow.AddHours(1), ct);

            return new XmlPullResult
            {
                XmlSourceId = xmlSourceId,
                Duration    = DateTime.UtcNow - start,
                Error       = ex.Message
            };
        }
    }
}

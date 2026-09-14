using System.Globalization;
using System.Xml.Linq;
using MultiSiteIkas.Core.Interfaces;
using Serilog;

namespace MultiSiteIkas.Core.Xml;

public sealed class XmlParsingService : IXmlParsingService
{
    public async Task<List<ParsedProduct>> ParseAsync(Stream xmlStream, CancellationToken ct = default)
    {
        var doc = await XDocument.LoadAsync(xmlStream, LoadOptions.None, ct);
        var results = new List<ParsedProduct>();
        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in doc.Root!.Elements("product"))
        {
            try
            {
                foreach (var parsed in ParseProduct(el, seenSkus))
                    results.Add(parsed);
            }
            catch (Exception ex)
            {
                Log.Warning("Skipping product {Id}: {Reason}",
                    el.Element("id")?.Value, ex.Message);
            }
        }

        return results;
    }

    private static IEnumerable<ParsedProduct> ParseProduct(XElement el, HashSet<string> seenSkus)
    {
        var productId = el.Element("id")?.Value?.Trim() ?? string.Empty;

        var name = el.Element("name")?.Value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) yield break;

        // Category: <category_path> (dolceev) → <categories><category><name> (tanadore)
        var categoryPath = el.Element("category_path")?.Value?.Trim();
        if (string.IsNullOrEmpty(categoryPath))
        {
            categoryPath = el.Element("categories")?
                .Elements("category")
                .FirstOrDefault()?
                .Elements("name")
                .FirstOrDefault()?.Value?.Trim()
                ?? string.Empty;
        }

        var brand = el.Element("brand")?.Element("name")?.Value?.Trim();
        var description = el.Element("description")?.Value;

        var variants = el.Element("variants")?.Elements("variant").ToList();
        if (variants is null || variants.Count == 0) yield break;

        var variantIndex = 0;
        foreach (var variant in variants)
        {
            var rawSku = variant.Element("sku")?.Value?.Trim() ?? string.Empty;
            // Boş SKU'ya fallback: productId-variantIndex
            var sku = string.IsNullOrEmpty(rawSku) ? $"{productId}-{variantIndex}" : rawSku;

            // Aynı SKU'yu iki kez alma (aynı ürünün tüm beden/renk variantları aynı SKU paylaşıyorsa ilkini al)
            if (!seenSkus.Add(sku))
            {
                variantIndex++;
                continue;
            }

            var variantId = variant.Element("id")?.Value?.Trim();

            // Görseller — <imageUrl> (mp4 videoları atla)
            var images = variant.Element("images")?
                .Elements("image")
                .OrderBy(i => ParseInt(i.Element("order")?.Value))
                .Select(i => i.Element("imageUrl")?.Value?.Trim())
                .Where(u => !string.IsNullOrEmpty(u) && !u!.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                .Cast<string>()
                .ToList() ?? new List<string>();

            // Fiyat
            var priceEl = variant.Element("prices")?.Elements("price").FirstOrDefault();
            var sellPrice = ParseDecimal(priceEl?.Element("sellPrice")?.Value);
            var discountPrice = ParseDecimalNullable(priceEl?.Element("discountPrice")?.Value);
            var currency = priceEl?.Element("currency")?.Value?.Trim() ?? "TRY";

            if (sellPrice <= 0 && (discountPrice ?? 0) <= 0)
            {
                Log.Warning("Skipping variant {Sku}: no valid price", sku);
                variantIndex++;
                continue;
            }

            // Stok — tüm depoların toplamı
            var stockCount = variant.Element("stocks")?
                .Elements("stock")
                .Sum(s => ParseInt(s.Element("stockCount")?.Value)) ?? 0;

            // Ağırlık — desi alanı
            var weight = ParseDecimalNullable(variant.Element("desi")?.Value);

            // Variant özellikleri (Renk, Beden vs.)
            var attributes = variant.Element("variantValues")?
                .Elements("variantValue")
                .Where(v => !string.IsNullOrEmpty(v.Element("variantValueName")?.Value))
                .ToDictionary(
                    v => v.Element("variantTypeName")?.Value?.Trim() ?? "Özellik",
                    v => v.Element("variantValueName")?.Value?.Trim() ?? string.Empty)
                ?? new Dictionary<string, string>();

            yield return new ParsedProduct
            {
                ExternalId  = variantId ?? productId,
                Sku         = sku,
                Name        = name,
                Description = description,
                CategoryPath = categoryPath,
                Brand       = brand,
                OriginalPrice = sellPrice,
                SalePrice   = discountPrice.HasValue && discountPrice > 0 ? discountPrice.Value : sellPrice,
                DiscountPrice = discountPrice,
                Currency    = currency,
                StockQuantity = stockCount,
                Weight      = weight,
                Images      = images,
                Attributes  = attributes
            };

            variantIndex++;
        }
    }

    private static decimal ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static decimal? ParseDecimalNullable(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static int ParseInt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return int.TryParse(s, out var i) ? i : 0;
    }
}

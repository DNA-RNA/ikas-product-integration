using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MultiSiteIkas.Core.Interfaces;
using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Core.Ikas;

public sealed partial class IkasFieldMapper : IIkasFieldMapper
{
    private readonly IPricingService _pricing;

    public IkasFieldMapper(IPricingService pricing) => _pricing = pricing;

    public IkasProductInput Map(IReadOnlyList<Product> variants, SiteMapping mapping, string? categoryName)
    {
        var first = variants[0];

        var brandMappings = ParseJson<Dictionary<string, string>>(mapping.BrandMappings);
        var brandName = ResolveBrandName(first.Brand, brandMappings);

        return new IkasProductInput
        {
            Name         = Truncate(first.Name, 1000),
            Description  = WebUtility.HtmlDecode(first.Description ?? string.Empty),
            Type         = "PHYSICAL",
            Weight       = first.Weight > 0 ? first.Weight : null,
            BrandName    = string.IsNullOrEmpty(brandName) ? null : brandName,
            CategoryName = string.IsNullOrEmpty(categoryName) ? null : categoryName,
            Variants     = variants.Select(p => MapVariant(p, mapping)).ToArray(),
            MetaSlug     = GenerateSlug(first.Name),
            MetaTitle    = Truncate(first.Name, 70),
            MetaDescription = TruncateText(first.Description, 160)
        };
    }

    private IkasVariantInput MapVariant(Product product, SiteMapping mapping)
    {
        var basePrice = product.SalePrice > 0 ? product.SalePrice : product.OriginalPrice;
        var finalPrice = _pricing.CalculateSitePrice(
            basePrice, mapping.PriceMarginPercentage, mapping.AdditionalPrice);

        var images = mapping.SendImages
            ? MapImages(product.ImagesJson)
            : Array.Empty<IkasImageInput>();

        var attributes = ParseJson<Dictionary<string, string>>(product.AttributesJson)
            ?? new Dictionary<string, string>();

        return new IkasVariantInput
        {
            Sku         = product.Sku,
            BarcodeList = string.IsNullOrEmpty(product.Barcode) ? null : new[] { product.Barcode },
            SellPrice   = finalPrice,
            IsActive    = product.StockQuantity > 0 || !mapping.DeactivateZeroStock,
            Images      = images,
            Weight      = product.Weight.HasValue && product.Weight > 0 ? (float)product.Weight.Value : null,
            Attributes  = attributes
        };
    }

    private static IkasImageInput[] MapImages(string? imagesJson)
    {
        if (string.IsNullOrEmpty(imagesJson)) return Array.Empty<IkasImageInput>();
        var urls = ParseJson<List<string>>(imagesJson);
        if (urls is null || urls.Count == 0) return Array.Empty<IkasImageInput>();

        return urls
            .Where(u => !string.IsNullOrEmpty(u))
            .Select((url, i) => new IkasImageInput
            {
                Order    = i + 1,
                IsMain   = i == 0,
                FileName = url
            })
            .ToArray();
    }

    private static string? ResolveBrandName(string? sourceBrand, Dictionary<string, string>? mappings)
    {
        if (string.IsNullOrEmpty(sourceBrand)) return null;
        if (mappings != null && mappings.TryGetValue(sourceBrand, out var mapped)) return mapped;
        return sourceBrand;
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace("ş", "s").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ö", "o").Replace("ç", "c").Replace("ı", "i").Replace("İ", "i");
        slug = NonAlphanumericRegex().Replace(slug, " ");
        slug = MultiSpaceRegex().Replace(slug, "-");
        slug = MultipleDashRegex().Replace(slug, "-").Trim('-');
        return slug.Length > 100 ? slug[..100] : slug;
    }

    private static T? ParseJson<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return null; }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];

    private static string? TruncateText(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return s.Length <= max ? s : s[..max] + "...";
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleDashRegex();
}

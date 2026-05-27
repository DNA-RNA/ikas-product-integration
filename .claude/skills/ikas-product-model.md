# İkas Product Modeli ve XML → İkas Mapping

## İkas Product Şeması (Özet)

İkas variant-merkezli bir model kullanır: Her ürün en az 1 variant içerir. SKU, fiyat, stok ve görseller variant seviyesinde tutulur.

```json
{
  "id": null,
  "name": "Şeffaf Epoksi Reçine 1kg",
  "shortDescription": "Yüksek kaliteli şeffaf epoksi reçine",
  "description": "<p>Tam metin açıklama...</p>",
  "type": "PHYSICAL",
  "weight": 1.2,
  "brand": { "name": "EpoxyPro" },
  "productCategories": [
    { "categoryId": "cat_abc123" }
  ],
  "salesChannels": [
    { "id": "sc_default" }
  ],
  "variants": [
    {
      "id": null,
      "sku": "REC-EPO-001",
      "barcodeList": ["8690123456789"],
      "prices": [
        {
          "sellPrice": 155.25,
          "discountPrice": null,
          "buyPrice": null
        }
      ],
      "stocks": [
        { "stockCount": 50 }
      ],
      "images": [
        {
          "imageId": null,
          "order": 1,
          "isMain": true,
          "fileName": "https://hobizubi.com/images/rec-epo-001-1.jpg"
        }
      ],
      "isActive": true
    }
  ],
  "tags": ["epoksi", "reçine"],
  "metaData": {
    "slug": "seffaf-epoksi-recine-1kg",
    "metaTitle": "Şeffaf Epoksi Reçine 1kg",
    "metaDescription": "Yüksek kalite şeffaf epoksi..."
  }
}
```

## XML → İkas Mapping Tablosu

| Master XML | İkas Field | Dönüşüm | Zorunlu |
|-----------|-----------|---------|---------|
| `<sku>` | `variants[0].sku` | Direkt | ✅ |
| `<barcode>` | `variants[0].barcodeList[]` | Array'e tek eleman | ❌ |
| `<name>` | `name` | Direkt (≤1000 char) | ✅ |
| `<name>` | `metaData.slug` | Slug fonksiyonu | ✅ |
| `<name>` | `metaData.metaTitle` | Direkt | ❌ |
| `<description>` | `description` | HTML decode + truncate | ✅ |
| `<description>` | `shortDescription` | İlk 200 char + "..." | ❌ |
| `<description>` | `metaData.metaDescription` | İlk 160 char + "..." | ❌ |
| `<category>` | `productCategories[].categoryId` | Mapping ile çevir → categoryId çöz | ✅ |
| `<brand>` | `brand.name` | Mapping ile çevir veya direkt | ❌ |
| `<sale_price>` veya `<price>` | `variants[0].prices[0].sellPrice` | **Marj uygulanmış** | ✅ |
| `<price>` (orijinal) | Tags için "list-price-150" vb. (opsiyonel) | - | ❌ |
| `<currency>` | (Sales channel'da tanımlı) | - | - |
| `<stock>` | `variants[0].stocks[0].stockCount` | Negatif → 0 | ✅ |
| `<weight>` | `weight` | Direkt | ❌ |
| `<images><image>` | `variants[0].images[]` | Sırayla order ekle, ilki isMain | ❌ |
| `<attributes><attribute>` | `tags[]` veya `metaData.attributes` | Etiket olarak | ❌ |

## Field Mapper Implementasyonu

```csharp
public sealed class IkasFieldMapper(
    IPricingService pricingService,
    ICategoryResolver categoryResolver) : IIkasFieldMapper
{
    public IkasProductInput MapToIkasProduct(Product product, SiteMapping mapping)
    {
        // Fiyat hesapla
        var basePrice = product.SalePrice > 0 ? product.SalePrice : product.OriginalPrice;
        var finalPrice = pricingService.CalculateSitePrice(
            basePrice,
            mapping.PriceMarginPercentage,
            mapping.AdditionalPrice);

        // Kategori çöz
        var categoryName = ResolveCategoryName(product.CategoryPath, mapping.CategoryMappings);
        // Not: categoryId resolver bunu hedef mağazadaki ID'ye çevirmeli (cache'li)
        var categoryId = categoryResolver.ResolveId(mapping.Id, categoryName);

        // Marka çöz
        var brandName = ResolveBrandName(product.Brand, mapping.BrandMappings);

        // İmages
        var images = mapping.SendImages
            ? MapImages(product.Images)
            : new List<IkasImage>();

        return new IkasProductInput
        {
            Id = null, // upsert sırasında belirlenecek
            Name = TruncateName(product.Name, 1000),
            ShortDescription = TruncateDescription(product.Description, 200),
            Description = HtmlDecode(product.Description),
            Type = "PHYSICAL",
            Weight = product.Weight ?? 0,
            Brand = string.IsNullOrEmpty(brandName) ? null : new IkasBrandRef { Name = brandName },
            ProductCategories = categoryId is not null
                ? new[] { new IkasCategoryRef { CategoryId = categoryId } }
                : Array.Empty<IkasCategoryRef>(),
            Variants = new[]
            {
                new IkasVariantInput
                {
                    Sku = product.Sku,
                    BarcodeList = string.IsNullOrEmpty(product.Barcode)
                        ? Array.Empty<string>()
                        : new[] { product.Barcode },
                    Prices = new[]
                    {
                        new IkasPriceInput
                        {
                            SellPrice = finalPrice,
                            DiscountPrice = null
                        }
                    },
                    Stocks = new[]
                    {
                        new IkasStockInput { StockCount = product.StockQuantity }
                    },
                    Images = images,
                    IsActive = product.StockQuantity > 0 || !mapping.DeactivateZeroStock
                }
            },
            MetaData = new IkasMetaData
            {
                Slug = GenerateSlug(product.Name),
                MetaTitle = TruncateName(product.Name, 70),
                MetaDescription = TruncateDescription(product.Description, 160)
            }
        };
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace("ş", "s").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ö", "o").Replace("ç", "c").Replace("ı", "i").Replace("İ", "i");
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');
        return slug.Length > 100 ? slug[..100] : slug;
    }

    private static string ResolveCategoryName(string sourcePath, Dictionary<string, string>? mappings)
    {
        if (mappings != null && mappings.TryGetValue(sourcePath, out var mapped))
            return mapped;
        // Fallback: son segment
        return sourcePath.Split('>').Last().Trim();
    }

    private static string? ResolveBrandName(string? sourceBrand, Dictionary<string, string>? mappings)
    {
        if (string.IsNullOrEmpty(sourceBrand)) return null;
        if (mappings != null && mappings.TryGetValue(sourceBrand, out var mapped)) return mapped;
        return sourceBrand;
    }

    private static List<IkasImage> MapImages(List<string> imageUrls) =>
        imageUrls.Select((url, index) => new IkasImage
        {
            Order = index + 1,
            IsMain = index == 0,
            FileName = url
        }).ToList();

    private static string TruncateName(string name, int max) =>
        name.Length <= max ? name : name[..max];

    private static string? TruncateDescription(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length <= max) return text;
        return text[..max] + "...";
    }

    private static string HtmlDecode(string? html) =>
        string.IsNullOrEmpty(html) ? string.Empty : WebUtility.HtmlDecode(html);
}
```

## Category Resolver (Cache'li)

Hedef mağazadaki kategori ID'lerini öğrenmek için:

```csharp
public interface ICategoryResolver
{
    Task<string?> ResolveIdAsync(long siteMappingId, string categoryName, CancellationToken ct);
    Task RefreshAsync(long siteMappingId, CancellationToken ct);
}
```

İlk çağrıda hedef mağazanın tüm kategorilerini İkas API ile çek, in-memory cache'le. 1 saat TTL.
Bulunmazsa: ya `saveCategory` mutation'ı ile oluştur, ya null dön (admin manuel müdahale etsin).

## Upsert Mantığı (TransferService içinde)

```
1. product_transfers tablosundan bu (source_product_id, target_company_id) pair'ini ara
2. Var ve ikas_product_id varsa:
   - IkasProductInput.Id = ikas_product_id (UPDATE path)
3. Yoksa:
   - Önce İkas API'de listProduct ile SKU ara (manuel müdahale durumlarına karşı)
   - Bulunursa o ID'yi kullan (UPDATE) ve transfers tablosuna kaydet
   - Bulunmazsa Id = null bırak (CREATE)
4. saveProduct mutation'ını çağır
5. Dönen response'taki id'yi product_transfers.ikas_product_id'ye yaz
6. Status = Success, dates güncelle
```

## "Pasifleştirme" Davranışı

Bir ürün master'da silinirse veya stok 0'a düşerse hedef mağazada ne olacak?

- **Stok 0 + `deactivate_zero_stock=true`**: `variants[0].isActive = false` (ürün satışta görünmez ama silinmez).
- **Master'dan silinme (XML'de artık yok)**:
  - `products.last_seen_date` X gün geçmişse o ürünü hedef mağazada `isActive=false` yap.
  - Asla silme — geçmiş veri ve referans önemli.

## Çok Dilli İçerik (mallofmolds.com)

`mallofmolds.com` İngilizce. Çözümler:
1. **Manuel çeviri**: `products.name_en` ve `products.description_en` kolonları ekle, admin doldurur. Mapper bu mağaza için EN versiyonunu kullanır.
2. **Otomatik çeviri**: Faz 2'de DeepL/Google Translate API entegrasyonu.
3. **Master'da iki dil**: Master mağazanın description'ında `<en>...</en><tr>...</tr>` gibi yapı, parser parse eder.

Faz 1 için **manuel çeviri tablosu** öneriliyor: `product_translations(product_id, lang, name, description)`. Boşsa ürün mallofmolds'a transfer edilmez (skip + log).

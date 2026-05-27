# XML Feed Format — hobizubi.com (İkas Exporter)

## Kaynak

XML, master mağazada (`hobizubi.com`) **İkas Exporter** uygulaması tarafından oluşturulur. Sonuç bir URL'dir:

```
https://ikas-exporter-app.ikasapps.com/api/exports/{tenantId}/{exportId}.xml?
    templateType=1
    &showCategoryPath=false
    &showTotalStockCount=false
    &imageExtensionJPEG=false
    &showDiscountPrice=false
    &showPriceInfo=false
    &separateCategories=false
```

Bu URL `xml_sources.xml_url` kolonunda saklanır. Sistem GET isteğiyle XML'i indirir.

## XML Şeması

```xml
<?xml version="1.0" encoding="UTF-8"?>
<products>
    <product>
        <id>12345</id>                          <!-- master mağaza internal ID -->
        <sku>REC-EPO-001</sku>                  <!-- ZORUNLU, anahtar -->
        <barcode>8690123456789</barcode>
        <name>Şeffaf Epoksi Reçine 1kg</name>
        <description><![CDATA[Yüksek kaliteli şeffaf epoksi reçine...]]></description>
        <category>Hobi > Reçine > Epoksi</category>   <!-- breadcrumb path -->
        <brand>EpoxyPro</brand>
        <price>150.00</price>                   <!-- liste/orijinal fiyat -->
        <sale_price>135.00</sale_price>         <!-- güncel satış fiyatı -->
        <discount_price>120.00</discount_price> <!-- kampanya fiyatı (opsiyonel) -->
        <currency>TRY</currency>
        <stock>50</stock>
        <weight>1.2</weight>
        <images>
            <image>https://hobizubi.com/images/products/rec-epo-001-1.jpg</image>
            <image>https://hobizubi.com/images/products/rec-epo-001-2.jpg</image>
        </images>
        <attributes>
            <attribute name="Renk">Şeffaf</attribute>
            <attribute name="Ağırlık">1kg</attribute>
        </attributes>
    </product>
    <!-- daha fazla ürün -->
</products>
```

> NOT: Şema İkas Exporter'ın URL parametrelerine göre değişir. Yukarıdaki örnek `templateType=1` ile beklenen formattır. Gerçek bir XML'le doğrula. `showCategoryPath=false` parametresi varken `<category>` alanı yine de breadcrumb olabilir veya sadece son kategori olabilir — test et.

## Parse Stratejisi

### Küçük feed (<10K ürün): XDocument
```csharp
public async Task<List<ParsedProduct>> ParseAsync(Stream xmlStream, CancellationToken ct)
{
    var doc = await XDocument.LoadAsync(xmlStream, LoadOptions.None, ct);
    var products = doc.Root!
        .Elements("product")
        .Select(ParseOne)
        .Where(p => !string.IsNullOrEmpty(p.Sku))
        .ToList();
    return products;
}
```

### Büyük feed (10K+): XmlReader streaming
```csharp
public async IAsyncEnumerable<ParsedProduct> ParseStreamAsync(
    Stream xmlStream,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    using var reader = XmlReader.Create(xmlStream, new XmlReaderSettings { Async = true });
    while (await reader.ReadAsync())
    {
        if (reader is { NodeType: XmlNodeType.Element, Name: "product" })
        {
            var element = (XElement)await Task.Run(() => XNode.ReadFrom(reader), ct);
            yield return ParseOne(element);
        }
    }
}
```

## ParseOne Method

```csharp
private ParsedProduct ParseOne(XElement el)
{
    var culture = CultureInfo.InvariantCulture;

    var sku = el.Element("sku")?.Value?.Trim() ?? string.Empty;
    if (string.IsNullOrEmpty(sku))
        throw new XmlValidationException("Product missing SKU");

    var price = ParseDecimal(el.Element("price")?.Value, culture);
    var salePrice = ParseDecimal(el.Element("sale_price")?.Value, culture);
    if (salePrice <= 0) salePrice = price; // sale_price yoksa price kullan

    return new ParsedProduct
    {
        ExternalId = el.Element("id")?.Value,
        Sku = sku,
        Barcode = el.Element("barcode")?.Value?.Trim(),
        Name = el.Element("name")?.Value?.Trim() ?? throw new XmlValidationException($"{sku}: missing name"),
        Description = el.Element("description")?.Value,
        CategoryPath = el.Element("category")?.Value?.Trim() ?? string.Empty,
        Brand = el.Element("brand")?.Value?.Trim(),
        OriginalPrice = price,
        SalePrice = salePrice,
        DiscountPrice = ParseDecimal(el.Element("discount_price")?.Value, culture),
        Currency = el.Element("currency")?.Value?.Trim() ?? "TRY",
        StockQuantity = Math.Max(0, ParseInt(el.Element("stock")?.Value)),
        Weight = ParseDecimal(el.Element("weight")?.Value, culture),
        Images = el.Element("images")?
            .Elements("image")
            .Select(i => i.Value.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList() ?? new(),
        Attributes = el.Element("attributes")?
            .Elements("attribute")
            .ToDictionary(
                a => a.Attribute("name")?.Value ?? "Unknown",
                a => a.Value.Trim()) ?? new()
    };
}

private static decimal ParseDecimal(string? s, CultureInfo culture)
{
    if (string.IsNullOrWhiteSpace(s)) return 0m;
    return decimal.TryParse(s, NumberStyles.Any, culture, out var d) ? d : 0m;
}

private static int ParseInt(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return 0;
    return int.TryParse(s, out var i) ? i : 0;
}
```

## Validation Kuralları

| Alan | Kural | Eksikse |
|------|-------|---------|
| `sku` | Zorunlu, boş değil, max 100 char | Skip + log |
| `name` | Zorunlu, max 1000 char | Skip + log |
| `price` veya `sale_price` | > 0 | Skip + log |
| `stock` | Negatif → 0 | Düzelt, devam |
| `category` | Zorunlu | Skip + log |
| `brand` | Opsiyonel | null, devam |
| `weight` | Opsiyonel, 0 olabilir | null, devam |
| `images` | Opsiyonel, boş olabilir | Boş liste, devam |

## ParsedProduct DTO

```csharp
public sealed class ParsedProduct
{
    public string? ExternalId { get; set; }
    public required string Sku { get; set; }
    public string? Barcode { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string CategoryPath { get; set; }
    public string? Brand { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string Currency { get; set; } = "TRY";
    public int StockQuantity { get; set; }
    public decimal? Weight { get; set; }
    public List<string> Images { get; set; } = new();
    public Dictionary<string, string> Attributes { get; set; } = new();
}
```

`Product` entity'ye dönüşüm `XmlParsingService` içinde yapılır.

## Indirme

```csharp
public async Task<Stream> DownloadXmlAsync(string url, CancellationToken ct)
{
    using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
    response.EnsureSuccessStatusCode();
    var memoryStream = new MemoryStream();
    await response.Content.CopyToAsync(memoryStream, ct);
    memoryStream.Position = 0;
    return memoryStream;
}
```

> Production'da timeout 5 dakika, retry 3 (60s/300s/900s). Polly ile sarmalı.

## Edge Case'ler

- **Aynı SKU birden fazla `<product>` içinde**: Sonuncusu kazanır, warning log.
- **Boş `<images>`**: Ürün resimsiz transfer edilir.
- **Bozuk encoding**: UTF-8 olmayan karakter → `HttpResponseHeaders` ile encoding tespit et, `StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true)`.
- **Çok büyük dosya (>100MB)**: Streaming versiyona geç, batch insert kullan.
- **Network timeout**: 5 dakika sonra exception, Polly retry alır.

## Test Verisi
Geliştirme için küçük bir mock XML hazırla, `MultiSiteIkas.Tests/TestData/sample_products.xml` olarak commit et. 5-10 farklı ürün, farklı kategoriler, eksik alanlar dahil edge case'ler.

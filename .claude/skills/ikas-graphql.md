# İkas Admin GraphQL API — Reference Skill

## Genel Bilgiler

- **Endpoint:** `https://api.myikas.com/api/v1/admin/graphql`
- **HTTP Method:** POST
- **Content-Type:** `application/json`
- **Authorization:** `Bearer {API_KEY}:{API_SECRET}`

API Key ve Secret nasıl alınır:
1. `https://builders.ikas.com` adresine git
2. Custom app oluştur
3. App'in scope'larına Product read/write yetkisi ver
4. Oluşan API Key ve Secret'i kopyala

> NOT: İkas API yapısı zaman zaman güncellenir. Bu skill referans niteliğinde — production'a almadan önce mutlaka İkas'ın resmi developer dokümantasyonu (`https://developer.myikas.com`) ile karşılaştır.

## GraphQL Request Anatomisi

```http
POST /api/v1/admin/graphql HTTP/1.1
Host: api.myikas.com
Content-Type: application/json
Authorization: Bearer sk_live_abc123:secret_xyz789

{
  "query": "...",
  "variables": { ... },
  "operationName": "OptionalName"
}
```

Response:
```json
{
  "data": { ... },
  "errors": [{ "message": "...", "extensions": { ... } }]
}
```

## Önemli Operasyonlar

### 1) Ürünü SKU ile Ara

```graphql
query FindProductBySku($sku: StringFilterInput) {
  listProduct(sku: $sku, pagination: { limit: 1, page: 1 }) {
    data {
      id
      name
      sku
      variants {
        id
        sku
        prices {
          sellPrice
          discountPrice
        }
        stocks {
          stockCount
        }
      }
    }
    count
    page
    limit
  }
}
```

Variables:
```json
{ "sku": { "eq": "REC-EPO-001" } }
```

> NOT: İkas GraphQL'de filter input'ların kesin syntax'ı sürüme göre değişebilir. `StringFilterInput`, `eq`/`contains`/`in` operatörlerinden birini destekler. İlk entegrasyonda Postman ile keşfet.

### 2) Ürün Create/Update (saveProduct mutation)

```graphql
mutation SaveProduct($input: ProductInput!) {
  saveProduct(input: $input) {
    id
    name
    sku
    variants { id sku }
  }
}
```

Create için `input.id` boş bırakılır. Update için `input.id` mevcut İkas product ID'si verilir.

Örnek `ProductInput`:
```json
{
  "id": null,
  "name": "Şeffaf Epoksi Reçine 1kg",
  "shortDescription": "Yüksek kaliteli şeffaf epoksi",
  "description": "Tam metin açıklama...",
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
      "sku": "REC-EPO-001",
      "barcodeList": ["8690123456789"],
      "prices": [
        { "sellPrice": 135.00, "discountPrice": null }
      ],
      "stocks": [
        { "stockCount": 50 }
      ],
      "images": [
        { "imageId": null, "order": 1, "isMain": true, "fileName": "https://.../1.jpg" },
        { "imageId": null, "order": 2, "isMain": false, "fileName": "https://.../2.jpg" }
      ],
      "isActive": true
    }
  ]
}
```

> İkas variant-merkezli bir model. Basit (varyantsız) bir ürün bile en az **1 variant** içerir. SKU/fiyat/stok variant seviyesinde.

### 3) Kategori Listele

```graphql
query ListCategories {
  listCategory(pagination: { limit: 100, page: 1 }) {
    data { id name parentId }
    count page limit
  }
}
```

### 4) Marka Listele

```graphql
query ListBrands {
  listBrand(pagination: { limit: 100, page: 1 }) {
    data { id name }
    count page limit
  }
}
```

### 5) Marka Oluştur (gerekirse)

```graphql
mutation SaveBrand($input: BrandInput!) {
  saveBrand(input: $input) {
    id name
  }
}
```

## C# Client Iskeleti

```csharp
public interface IIkasApiService
{
    Task<IkasProduct?> FindBySkuAsync(IkasCredentials creds, string sku, CancellationToken ct);
    Task<IkasProduct> SaveProductAsync(IkasCredentials creds, IkasProductInput input, CancellationToken ct);
    Task<IReadOnlyList<IkasCategory>> ListCategoriesAsync(IkasCredentials creds, CancellationToken ct);
    Task<IReadOnlyList<IkasBrand>> ListBrandsAsync(IkasCredentials creds, CancellationToken ct);
}

public sealed record IkasCredentials(string ApiKey, string ApiSecret, string StoreCode);

internal sealed class GraphQLRequest
{
    public required string Query { get; init; }
    public object? Variables { get; init; }
    public string? OperationName { get; init; }
}

internal sealed class GraphQLResponse<T>
{
    public T? Data { get; init; }
    public List<GraphQLError>? Errors { get; init; }
}

internal sealed class GraphQLError
{
    public required string Message { get; init; }
    public Dictionary<string, object>? Extensions { get; init; }
}
```

## Service Implementasyon Pattern

```csharp
public sealed class IkasApiService(
    HttpClient http,
    ILogger<IkasApiService> logger) : IIkasApiService
{
    private const string Endpoint = "/api/v1/admin/graphql";

    public async Task<IkasProduct> SaveProductAsync(
        IkasCredentials creds,
        IkasProductInput input,
        CancellationToken ct)
    {
        var request = new GraphQLRequest
        {
            Query = GraphQLQueries.SaveProduct,
            Variables = new { input }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", $"{creds.ApiKey}:{creds.ApiSecret}");

        var response = await http.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("[{Store}] İkas API HTTP {Code}: {Body}",
                creds.StoreCode, response.StatusCode, body);
            throw new IkasApiException(creds.StoreCode, response.StatusCode,
                $"HTTP {(int)response.StatusCode}: {body}");
        }

        var gqlResponse = await response.Content
            .ReadFromJsonAsync<GraphQLResponse<SaveProductData>>(cancellationToken: ct)
            ?? throw new IkasApiException(creds.StoreCode, response.StatusCode, "Empty response");

        if (gqlResponse.Errors is { Count: > 0 })
        {
            var msg = string.Join("; ", gqlResponse.Errors.Select(e => e.Message));
            throw new IkasApiException(creds.StoreCode, response.StatusCode, msg);
        }

        return gqlResponse.Data!.SaveProduct;
    }
}
```

## Hata Kodları

| Status | Anlam | Aksiyon |
|--------|-------|---------|
| 200 + errors[] | GraphQL hatası | Errors içeriğini parse et, IkasApiException fırlat |
| 401 | Auth başarısız | Kritik, retry etme, müdahale gerekli |
| 403 | Yetkisiz scope | App scope'larını kontrol et |
| 429 | Rate limit | Polly retry + Retry-After header'a saygı |
| 500/502/503/504 | Geçici | Polly retry (exponential backoff) |
| 400 | Validasyon | Retry etme, log + skip |

## Rate Limit

İkas'ın resmi rate limit dokümantasyonu netleştirilmeli. Genel kabul:
- ~60-120 request/dakika per app.
- 429 dönerse `Retry-After` header'a uy.
- 4 paralel hedef mağaza ile çalışırken her biri kendi rate'iyle sınırlı (farklı app'ler).
- Tek mağaza içinde 5'ten fazla paralel request açmama önerisi.

## Bilinen Tuzaklar

1. **Variant zorunluluğu**: Basit ürün bile 1 variant ile gönderilir.
2. **Image upload**: URL'den image referansı verirsen İkas kendi CDN'ine kopyalar. Image upload mutation'ı da var, gerekirse.
3. **Category create**: Kategori yoksa otomatik oluşmaz. Önce manuel veya `saveCategory` ile yarat.
4. **Brand auto-create**: Bazı sürümlerde brand isim verince otomatik yaratılır, bazılarında ID gerekir. Test et.
5. **Slug çakışması**: Aynı slug'lı ürün varsa 400 dönüyor. Unique olmasını sağla.

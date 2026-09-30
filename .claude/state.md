# Multi-Site İkas Ürün Dağıtım — Proje Durumu

## Amaç
Kaynak İkas mağazalarının XML feed'lerini çekip kategori bazlı filtreleyerek hedef İkas mağazalarına otomatik aktarmak.

## Mağaza Haritası

| Rol | Site | Kategori Odağı |
|-----|------|----------------|
| Master | **hobizubi.com** | Tüm kategoriler (XML kaynağı) |
| Hedef 1 | **recinem.com** | Reçine, Epoksi, Pigment |
| Hedef 2 | **boncukpasaji.com** | Boncuk, İp/Tel, Takı Malzemeleri |
| Hedef 3 | **kalipatolyesi.com** | Silikon Kalıp, Reçine Kalıp |
| Hedef 4 | **mallofmolds.com** | Silicone Molds, Resin Molds (İngilizce katalog) |
| Test Kaynak | **tanadore.com** | Stepne Kılıfı |
| Test Hedef | **dolceev.com** | Stepne kılıfları → "Step Kılıflar" kategorisi |

## XML Kaynağı
- URL: İkas Exporter app tarafından oluşturulmuş URL
- Format: `<products><product><variants><variant>...</variant></variants></product></products>`
- Kategori: `<category_path>` (showCategoryPath=true) veya `<categories><category><name>` (false)
- Bu URL `xml_sources` tablosunda saklanır.

## İkas API Bağlantısı (Hedef Mağazalar)
- Her hedef mağaza için **builders.ikas.com** üzerinden özel uygulama (private app) oluşturulur.
- Uygulama: `client_id` ve `client_secret` alınır.
- Auth: OAuth2 client_credentials akışı → `POST /api/admin/oauth/token` → `Bearer {access_token}`
- GraphQL Endpoint: `https://api.myikas.com/api/v2/admin/graphql`
- Token 4 saat geçerli, in-memory cache'leniyor.

## Teknoloji Yığını
- **Backend:** .NET 8 (ASP.NET Core Web API)
- **Database:** PostgreSQL (DBeaver ile yönetim)
- **ORM/Data:** Dapper (raw queries, snake_case mapping)
- **HTTP:** HttpClientFactory
- **XML:** System.Xml.Linq (XDocument)
- **Logging:** Serilog → Console + File

## Mimari (Solution Yapısı)
```
MultiSiteIkas.sln
├── MultiSiteIkas.API/         ASP.NET Core Web API + Test endpoint'leri
├── MultiSiteIkas.Core/        Business logic, services, DTO'lar
├── MultiSiteIkas.Data/        Dapper repository'ler, entity'ler
├── MultiSiteIkas.Jobs/        (Hangfire — Faz 2)
└── MultiSiteIkas.Tests/       (xUnit — Faz 2)
```

## Veri Akışı
1. **XML Pull** (`POST /api/test/xml-pull/{xmlSourceId}`)
   - `xml_sources` tablosundan URL al → HTTP GET
   - Parse et → her `<product>` altındaki her `<variant>` → ayrı `products` satırı
   - SKU boşsa `{productId}-{index}` oluşturulur
   - `products` tablosuna upsert (SKU bazlı idempotent)

2. **Transfer** (`POST /api/test/transfer/{siteMappingId}`)
   - `products`'ı `category_filters`'a göre filtrele (NULL = tümünü gönder)
   - Aynı isme sahip satırları grupla → 1 grup = 1 İkas ürünü
   - `category_mappings` ile kategori adı çözümlenir (kaynak → hedef adı)
   - `createProduct` / `updateProduct` ile İkas v2 API'ye gönder
   - Varyant özellikleri (`Renk`, `Beden`) variantValues olarak aktarılır
   - Sonuç `product_transfers` ve `transfer_logs` tablolarına yazılır

## Veri Modeli
- `companies` — site/firma kayıtları + `ikas_api_key` / `ikas_api_secret`
- `xml_sources` — XML feed URL'leri ve sync zamanları
- `site_mappings` — kaynak→hedef eşlemesi, fiyat marjı, kategori filtre/mapping JSON
- `products` — XML'den parse edilmiş ürünler (variant başına 1 satır)
- `product_transfers` — hangi ürünün hangi hedefe gittiği + İkas product/variant ID
- `transfer_logs` — her transfer job çalışmasının özeti

## İlerleme Durumu

### Faz 1 — Temel Altyapı ✅
- [x] Solution + projeler iskeleti
- [x] Entity'ler (Company, XmlSource, SiteMapping, Product, ProductTransfer, TransferLog)
- [x] Repository interface'leri + implementasyonları (tüm 6 adet)
- [x] DbConnectionFactory → PostgresConnectionFactory (Npgsql 8.0.5)
- [x] InitialCreate.sql → PostgreSQL syntax (BIGSERIAL, BOOLEAN, TIMESTAMP, ON CONFLICT DO UPDATE)
- [x] ProductRepository — ON CONFLICT (xml_source_id, sku) DO UPDATE upsert

### Faz 1 — XML Katmanı ✅
- [x] XmlParsingService — İkas Exporter XML formatı (variant-bazlı)
  - `<category_path>` veya `<categories><category><name>` → otomatik tespit
  - `<imageUrl>` alanı
  - Boş SKU → `{productId}-{index}` fallback
  - Aynı SKU'ya sahip varyantlarda ilki alınır (duplicate skip)
- [x] IXmlPullService + XmlPullService — URL'den indir, parse et, DB'ye upsert

### Faz 1 — Core Servisler ✅
- [x] CategoryFilterService — NULL filtre = tümünü geçir; wildcard/exact/substring
- [x] PricingService — marj + sabit fiyat
- [x] IkasApiService — OAuth2 token cache + v2 GraphQL
  - `createProduct(CreateProductInput!)` / `updateProduct(UpdateProductInput!)`
  - `listCategory` — düz array, pagination yok
  - Token 4 saatlik, static ConcurrentDictionary cache
- [x] IkasFieldMapper — IReadOnlyList<Product> → IkasProductInput (multi-variant)
- [x] TransferService — ürünleri isme göre grupla → tek İkas ürünü + çoklu varyant

### Faz 1 — DI + Test Altyapısı ✅
- [x] Program.cs — tüm DI kayıtları
- [x] appsettings.json — PostgreSQL connection string
- [x] TestController — test endpoint'leri:
  - `GET /api/test/xml-sources`
  - `GET /api/test/site-mappings`
  - `POST /api/test/xml-pull/{id}`
  - `GET /api/test/product-categories/{id}`
  - `GET /api/test/ikas-categories/{companyId}`
  - `GET /api/test/ikas-schema/{companyId}` (introspection)
  - `POST /api/test/transfer/{id}`
  - `GET /api/test/products/{xmlSourceId}`
  - `GET /api/test/xml-preview/{xmlSourceId}`

### Faz 2 — Job Engine + API (sonraki adım)
- [ ] Hangfire kurulum + dashboard auth
- [ ] XmlPullJob (Hangfire recurring)
- [ ] TransferJob (per-site mapping)
- [ ] REST API controller'ları (manual trigger, status endpoints)
- [ ] React dashboard
- [ ] Docker compose + production deployment

## Önemli Notlar — Öğrenilen Dersler

### İkas API v2 Farklılıkları
- `saveProduct` → yok; `createProduct(CreateProductInput!)` + `updateProduct(UpdateProductInput!)`
- `listCategory` → `pagination` argümanı yok, düz `[Category]` döner
- Varyant görseli: `imageUrl` (NOT `fileName`)
- Varyant özellikleri: `variantValues: [{ variantTypeName, variantValueName }]`
- Token endpoint: `POST /api/admin/oauth/token` → `client_credentials` grant
- GraphQL endpoint: `/api/v2/admin/graphql`
- `CreateHTMLMetaDataInput.slug` → REQUIRED (boş geçilemez)
- Stok yönetimi: `stocks` alanı `CreateProductVariantInput`'ta yok → ayrı mutation
- `ProductCategoryInput`: `{ name: String! }` (ID değil, isim gönderiliyor)

### XML Parsing
- İkas Exporter XML formatı: variant-bazlı, her `<product>` içinde `<variants>` listesi
- Tanadore tipi: Kategori `<categories><category><name>` içinde
- Dolceev tipi: Kategori `<category_path>` direkt product altında
- Çoklu `<name>` aynı `<category>` içinde olabilir → ilki alınır

### Mimari Kararlar
- DB: Variant başına 1 satır (products tablosu)
- Transfer: Aynı isme sahip satırlar gruplanır → 1 İkas ürünü + N varyant
- Category resolver kaldırıldı → v2 API kategori adı kabul ediyor (ID gerekmez)

## Konfigürasyon
- API Key/Secret: `companies` tablosunda `ikas_api_key`, `ikas_api_secret`
- XML URL: `xml_sources` tablosunda `xml_url`
- Kategori mapping: `site_mappings.category_mappings` JSON → `{"kaynak adı": "hedef adı"}`
- Kategori filtresi: `site_mappings.category_filters` JSON array → NULL = tümünü gönder
- Fiyat marjı: `site_mappings.price_margin_percentage`

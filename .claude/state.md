# Multi-Site İkas Ürün Dağıtım — Proje Durumu

## Amaç
**hobizubi.com** (master mağaza) üzerindeki ürünleri kategori bazlı filtreleyerek 4 farklı İkas mağazasına otomatik aktarmak.

XML kaynağı master mağazada İkas Exporter uygulaması ile oluşturulmuş bir URL. Bu URL düzenli aralıklarla okunur, kategorilere göre filtrelenir, fiyat marjı uygulanır ve İkas Admin API üzerinden hedef mağazalara push edilir.

## Mağaza Haritası

| Rol | Site | Kategori Odağı |
|-----|------|----------------|
| Master | **hobizubi.com** | Tüm kategoriler (XML kaynağı) |
| Hedef 1 | **recinem.com** | Reçine, Epoksi, Pigment |
| Hedef 2 | **boncukpasaji.com** | Boncuk, İp/Tel, Takı Malzemeleri |
| Hedef 3 | **kalipatolyesi.com** | Silikon Kalıp, Reçine Kalıp |
| Hedef 4 | **mallofmolds.com** | Silicone Molds, Resin Molds (İngilizce katalog) |

## XML Kaynağı
- URL: İkas Exporter app tarafından oluşturulmuş URL (her master için 1 tane)
- Örnek pattern: `https://ikas-exporter-app.ikasapps.com/api/exports/{tenantId}/{exportId}.xml?templateType=1&...`
- Bu URL `xml_sources` tablosunda saklanır.
- Sistem URL'i GET ile çeker, XML'i parse eder.

## İkas API Bağlantısı (Hedef Mağazalar)
- Her hedef mağaza için **builders.ikas.com** üzerinden özel uygulama (custom app) oluşturulur.
- Uygulama oluşturulduktan sonra **API Key** ve **API Secret** alınır.
- Auth: `Authorization: Bearer {API_KEY}:{API_SECRET}` (Bearer formatında iki değer iki nokta üst üste ile ayrılır)
- Endpoint: `https://api.myikas.com/api/v1/admin/graphql` (GraphQL)
- Her hedef mağazanın kendi API Key/Secret çifti var, `site_mappings` tablosunda saklanır.

## Teknoloji Yığını
- **Backend:** .NET 8 (ASP.NET Core Web API + Worker Service)
- **Database:** SQL Server (Microsoft SQL Server, dokümandaki schema MSSQL T-SQL)
- **ORM/Data:** Entity Framework Core + Dapper (raw queries için)
- **Job Engine:** Hangfire + Hangfire.SqlServer
- **HTTP:** HttpClientFactory + Polly (retry, circuit breaker, rate limit)
- **XML:** System.Xml.Linq (XDocument)
- **Logging:** Serilog → Console + File + Seq
- **Validation:** FluentValidation
- **Frontend (Faz 2):** React + Vite + Material UI (admin dashboard)
- **Container:** Docker + docker-compose

## Mimari (Solution Yapısı)
```
MultiSiteIkas.sln
├── MultiSiteIkas.API/         ASP.NET Core Web API + Hangfire Dashboard
├── MultiSiteIkas.Core/        Business logic, services, DTO'lar
├── MultiSiteIkas.Data/        EF Core DbContext, repository'ler
├── MultiSiteIkas.Jobs/        Hangfire job tanımları (interfaces in Core)
└── MultiSiteIkas.Tests/       xUnit testleri
```

## Veri Akışı
1. **XmlPullJob (Hangfire recurring, default: 24 saatte 1)**
   - `xml_sources` tablosundan aktif kaynakları al.
   - Her birinin URL'inden XML'i indir.
   - Parse et, `products` tablosuna upsert et (SKU bazlı).
2. **TransferJob (her hedef mağaza için ayrı)**
   - `site_mappings`'ten o mağazanın konfigürasyonunu oku (API key/secret, kategori filtreleri, fiyat marjı, kategori mapping).
   - `products`'ı kategori filtrelerine göre filtrele.
   - Her ürün için: fiyat marjını uygula → İkas Product modeline map et → İkas GraphQL API'ye gönder (create veya update).
   - Sonucu `product_transfers` ve `transfer_logs` tablolarına yaz.
3. **Hangfire Dashboard** (`/hangfire`) — job durumu, manuel tetikleme, retry.

## Veri Modeli (Özet)
- `companies` — site/firma kayıtları
- `xml_sources` — master mağaza XML URL'leri ve schedule
- `site_mappings` — hedef mağaza konfigürasyonları (api_key, api_secret, fiyat marjı, kategori filtreleri JSON, kategori mapping JSON)
- `products` — master XML'den parse edilmiş ürünler
- `product_transfers` — hangi ürünün hangi hedefe gittiği + İkas product ID
- `transfer_logs` — her transfer job çalışmasının özeti

(Detaylı şema: `skills/database-schema.md`)

## İlerleme Durumu
- [ ] Solution + projeler iskeleti
- [ ] SQL Server connection + DbContext + migration'lar
- [ ] Entity'ler ve model class'lar
- [ ] Repository katmanı (EF Core + Dapper)
- [ ] XML Parser servisi
- [ ] Kategori filtreleme servisi
- [ ] Fiyatlandırma servisi (site marjı uygulama)
- [ ] İkas GraphQL API client (auth, product mutations)
- [ ] Field mapper (XML Product → İkas Product)
- [ ] Transfer servisi (orkestratör)
- [ ] Hangfire kurulum + dashboard auth
- [ ] XmlPullJob
- [ ] TransferJob (per-site)
- [ ] REST API controller'ları
- [ ] React dashboard (Faz 2)
- [ ] Docker compose + production deployment

## Konfigürasyon (Geliştiriciler İçin)
- API Key/Secret asla koda yazılmaz, asla repoya commit edilmez.
- Dev: User Secrets (`dotnet user-secrets set "...`)
- Prod: Environment variables veya Azure Key Vault / AWS Secrets Manager
- `appsettings.Development.json` örneklerinde placeholder kullan.

## Açık Sorular / Karar Bekleyenler
- ✅ **Sync sıklığı:** 24 saatte 1
- ✅ **Silinen ürün davranışı:** master'da silinirse → is_deleted=1, status=passive, stok=0 yap
- ✅ **mallofmolds.com Çeviri:** manual çeviri mapping tablosu (site_mappings.category_mappings, Faz 2'de manuel doldurulacak)
- ✅ **Görseller:** master mağazanın CDN URL'leri direkt kullan (basit yöntem)
- ℹ️ **İkas API rate limit:** 60/dk varsayımıyla başlıyor, gerçek limit test edilince güncellenecek

## Önemli Notlar
- Bu projenin bütün referans implementasyonu `/docs/MULTI_SITE_IKAS_PROJECT_GUIDE.md` dosyasında (yüklendiğinde) bulunur. Şüphede kalınca oraya bak.
- **mallofmolds.com İngilizce** olduğu için ya kategori isimlerini çeviren bir mapping setine ihtiyaç var, ya da master mağazada İngilizce alanlar/etiketler olmalı.
- `site_mappings.category_filters` JSON formatı: `["Reçine", "Epoksi", "Boncuk > *"]` — wildcard "alt kategoriler dahil" anlamında.
- `site_mappings.category_mappings` JSON formatı: `{"Reçine": "Resin", "Epoksi": "Epoxy"}` — master kategorisi → hedef mağaza kategorisi.

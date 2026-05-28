# Multi-Site İkas Ürün Dağıtım Sistemi

Hobizubi.com (master mağaza) üzerindeki ürünleri kategori bazlı filtreleyerek 4 farklı İkas mağazasına otomatik olarak aktaran sistem.

## 📋 Hedef Mağazalar

| Site | Kategori Odağı |
|------|----------------|
| **recinem.com** | Reçine, Epoksi, Pigment |
| **boncukpasaji.com** | Boncuk, İp/Tel, Takı Malzemeleri |
| **kalipatolyesi.com** | Silikon Kalıp, Reçine Kalıp |
| **mallofmolds.com** | Silicone Molds, Resin Molds (EN) |

## 🏗️ Proje Yapısı

```
MultiSiteIkas/
├── MultiSiteIkas.API/         REST API + Hangfire Dashboard
├── MultiSiteIkas.Core/        Business logic, DTOs, interfaces
├── MultiSiteIkas.Data/        EF Core, repositories, entities
├── MultiSiteIkas.Jobs/        Hangfire job definitions
└── MultiSiteIkas.Tests/       xUnit unit tests
```

## 🚀 Başlangıç

### Gereksinimler
- .NET 8 SDK
- SQL Server (ya da LocalDB)

### Setup

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Create database (migrations)
dotnet ef database update --project MultiSiteIkas.Data

# Run API
dotnet run --project MultiSiteIkas.API
```

API akan tersedia di: `http://localhost:5000`
Hangfire Dashboard: `http://localhost:5000/hangfire`

### Environment Setup

```bash
# Initialize user secrets (for API key/secret)
cd MultiSiteIkas.API
dotnet user-secrets init
dotnet user-secrets set "MallofmoldsApiKey" "your-key-here"
dotnet user-secrets set "RecinemApiKey" "your-key-here"
# ... diğer mağazalar için
```

## 📝 Teknoloji Yığını

- **Backend:** .NET 8 + ASP.NET Core
- **Database:** SQL Server + Entity Framework Core
- **Job Scheduling:** Hangfire + Hangfire.SqlServer
- **XML Processing:** System.Xml.Linq
- **Logging:** Serilog
- **Validation:** FluentValidation
- **Testing:** xUnit + Moq
- **ORM Extras:** Dapper (raw queries), EFCore.BulkExtensions

## 🔄 Veri Akışı

1. **XmlPullJob** (24 saatte 1): Master XML → `products` tablosuna
2. **TransferJob** (per-site): Filtre → Fiyat marjı → İkas GraphQL API
3. **Logging:** `transfer_logs`, `product_transfers` tablolarına

## 📚 Referans

Detaylı teknik doküman: `docs/MULTI_SITE_IKAS_PROJECT_GUIDE.md`
Proje durumu: `.claude/state.md`

## 📄 Lisans

Tüm hakları saklıdır.

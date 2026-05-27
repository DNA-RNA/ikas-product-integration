# Multi-Site İkas Ürün Dağıtım Projesi - Kapsamlı Uygulama Rehberi

## 📋 İçindekiler

1. [Proje Genel Bakış](#proje-genel-bakış)
2. [XML ve İkas API Entegrasyonu](#xml-ve-ikas-api-entegrasyonu)
3. [Mimari Tasarım](#mimari-tasarım)
4. [Veritabanı Tasarımı](#veritabanı-tasarımı)
5. [C# Implementation](#c-implementation)
6. [Node.js Implementation](#nodejs-implementation)
7. [API Tasarımı](#api-tasarımı)
8. [Frontend Dashboard](#frontend-dashboard)
9. [Deployment ve DevOps](#deployment-ve-devops)
10. [Maliyet Analizi ve Budget Planlama](#maliyet-analizi-ve-budget-planlama)
11. [Test Stratejisi](#test-stratejisi)

---

## 🎯 Proje Genel Bakış

### Senaryo
**hobizubi.com** (master site) üzerindeki ürünleri kategori bazlı filtreleyerek 4 farklı İkas sitesine otomatik dağıtma sistemi.

### Siteler

| Site | Odak Alan | Kategori Örnekleri |
|------|-----------|-------------------|
| **hobizubi.com** | Master/Tedarikçi | Tüm kategoriler |
| **recinem.com** | Reçine/Epoksi | Reçine, Epoksi, Pigment |
| **boncukpasaji.com** | Boncuk/Takı | Boncuk, İp/Tel, Takı Malzemeleri |
| **kalipatolyesi.com** | Kalıp | Silikon Kalıp, Reçine Kalıp |
| **mallofmolds.com** | Kalıp (EN) | Silicone Molds, Resin Molds |

### Temel Özellikler
- ✅ Kategori bazlı akıllı filtreleme
- ✅ Çoklu site transfer (1 ürün → N site)
- ✅ Site bazlı fiyatlandırma (+%15, +%20 vb.)
- ✅ Günlük otomatik senkronizasyon
- ✅ Detaylı transfer izleme
- ✅ RESTful API
- ✅ Web Dashboard
- ✅ XML → İkas field mapping

---

## 🔄 XML ve İkas API Entegrasyonu

### XML Format Örneği (hobizubi.com)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<products>
    <product>
        <id>12345</id>
        <sku>REC-EPO-001</sku>
        <barcode>8690123456789</barcode>
        <name>Şeffaf Epoksi Reçine 1kg</name>
        <description><![CDATA[Yüksek kaliteli şeffaf epoksi reçine...]]></description>
        <category>Hobi > Reçine > Epoksi</category>
        <brand>EpoxyPro</brand>
        <price>150.00</price>
        <sale_price>135.00</sale_price>
        <discount_price>120.00</discount_price>
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
    <!-- ... daha fazla ürün -->
</products>
```

### İkas API Yapısı

#### Authentication
İkas API **sadece API Key ve API Secret** ile kimlik doğrulama yapar:

```http
POST /api/v1/products
Host: api.myikas.com
Content-Type: application/json
Authorization: Bearer <API_KEY>:<API_SECRET>
```

#### İkas Ürün Modeli

```json
{
  "name": "Şeffaf Epoksi Reçine 1kg",
  "slug": "seffaf-epoksi-recine-1kg",
  "sku": "REC-EPO-001",
  "barcode": "8690123456789",
  "description": "Yüksek kaliteli şeffaf epoksi reçine...",
  "shortDescription": "Şeffaf epoksi reçine 1kg paket",
  "price": 135.00,
  "comparePrice": 150.00,
  "costPrice": 100.00,
  "stock": {
    "quantity": 50,
    "trackInventory": true,
    "allowBackorder": false
  },
  "category": {
    "name": "Resin Products",
    "path": "/resin-products"
  },
  "brand": {
    "name": "EpoxyPro"
  },
  "images": [
    {
      "src": "https://hobizubi.com/images/products/rec-epo-001-1.jpg",
      "position": 1
    },
    {
      "src": "https://hobizubi.com/images/products/rec-epo-001-2.jpg",
      "position": 2
    }
  ],
  "variants": [],
  "options": [],
  "tags": ["epoksi", "reçine", "şeffaf"],
  "seo": {
    "title": "Şeffaf Epoksi Reçine 1kg",
    "description": "Yüksek kaliteli şeffaf epoksi reçine..."
  },
  "weight": {
    "value": 1.2,
    "unit": "kg"
  },
  "status": "active"
}
```

### XML → İkas Field Mapping Tablosu

| XML Field | İkas Field | Dönüşüm | Zorunlu |
|-----------|-----------|---------|---------|
| `<sku>` | `sku` | Direkt | ✅ Evet |
| `<barcode>` | `barcode` | Direkt | ❌ Hayır |
| `<name>` | `name` | Direkt | ✅ Evet |
| `<description>` | `description` | HTML decode | ✅ Evet |
| `<category>` | `category.name` | Mapping ile | ✅ Evet |
| `<brand>` | `brand.name` | Direkt | ❌ Hayır |
| `<sale_price>` | `price` | Site marjı uygula | ✅ Evet |
| `<price>` | `comparePrice` | Site marjı uygula | ❌ Hayır |
| `<discount_price>` | İgnore veya tag | - | ❌ Hayır |
| `<stock>` | `stock.quantity` | Direkt | ✅ Evet |
| `<weight>` | `weight.value` | Direkt | ❌ Hayır |
| `<images>` | `images[]` | Array dönüşümü | ❌ Hayır |

### Field Mapping Service Örneği

**C# Implementation:**
```csharp
public class IkasFieldMapper
{
    public IkasProductRequest MapXmlProductToIkas(Product xmlProduct, SiteMapping mapping)
    {
        return new IkasProductRequest
        {
            Name = xmlProduct.Name,
            Slug = GenerateSlug(xmlProduct.Name),
            Sku = xmlProduct.Sku,
            Barcode = xmlProduct.Barcode,
            Description = HtmlDecode(xmlProduct.Description),
            ShortDescription = TruncateDescription(xmlProduct.Description, 200),
            
            // Fiyatlandırma - Site bazlı marj uygulanmış
            Price = xmlProduct.SalePrice, // Zaten mapping'de hesaplandı
            ComparePrice = xmlProduct.OriginalPrice, // Zaten mapping'de hesaplandı
            
            // Stok
            Stock = new IkasStock
            {
                Quantity = xmlProduct.StockQuantity,
                TrackInventory = true,
                AllowBackorder = false
            },
            
            // Kategori - Mapping uygulanmış
            Category = new IkasCategory
            {
                Name = xmlProduct.CategoryPath, // Zaten mapping'de dönüştürüldü
                Path = GenerateCategoryPath(xmlProduct.CategoryPath)
            },
            
            // Marka
            Brand = string.IsNullOrEmpty(xmlProduct.Brand) 
                ? null 
                : new IkasBrand { Name = xmlProduct.Brand },
            
            // Görseller
            Images = MapImages(xmlProduct.Images),
            
            // Ağırlık
            Weight = xmlProduct.Weight.HasValue 
                ? new IkasWeight { Value = xmlProduct.Weight.Value, Unit = "kg" }
                : null,
            
            // Durum
            Status = xmlProduct.IsActive ? "active" : "draft",
            
            // SEO
            Seo = new IkasSeo
            {
                Title = xmlProduct.Name,
                Description = TruncateDescription(xmlProduct.Description, 160)
            }
        };
    }
    
    private string GenerateSlug(string name)
    {
        return name.ToLower()
            .Replace("ş", "s").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ö", "o").Replace("ç", "c").Replace("ı", "i")
            .Replace(" ", "-")
            .Replace("/", "-");
    }
    
    private string GenerateCategoryPath(string categoryName)
    {
        return "/" + GenerateSlug(categoryName);
    }
    
    private string HtmlDecode(string html)
    {
        return System.Web.HttpUtility.HtmlDecode(html);
    }
    
    private string TruncateDescription(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength) + "...";
    }
    
    private List<IkasImage> MapImages(List<string> imageUrls)
    {
        if (imageUrls == null || imageUrls.Count == 0)
            return new List<IkasImage>();
        
        return imageUrls.Select((url, index) => new IkasImage
        {
            Src = url,
            Position = index + 1
        }).ToList();
    }
}
```

**Node.js Implementation:**
```javascript
class IkasFieldMapper {
    mapXmlProductToIkas(xmlProduct, mapping) {
        return {
            name: xmlProduct.name,
            slug: this.generateSlug(xmlProduct.name),
            sku: xmlProduct.sku,
            barcode: xmlProduct.barcode,
            description: this.htmlDecode(xmlProduct.description),
            shortDescription: this.truncateDescription(xmlProduct.description, 200),
            
            // Fiyatlandırma
            price: xmlProduct.salePrice,
            comparePrice: xmlProduct.originalPrice,
            
            // Stok
            stock: {
                quantity: xmlProduct.stockQuantity,
                trackInventory: true,
                allowBackorder: false
            },
            
            // Kategori
            category: {
                name: xmlProduct.categoryPath,
                path: this.generateCategoryPath(xmlProduct.categoryPath)
            },
            
            // Marka
            brand: xmlProduct.brand ? { name: xmlProduct.brand } : null,
            
            // Görseller
            images: this.mapImages(xmlProduct.images),
            
            // Ağırlık
            weight: xmlProduct.weight ? { value: xmlProduct.weight, unit: 'kg' } : null,
            
            // Durum
            status: xmlProduct.isActive ? 'active' : 'draft',
            
            // SEO
            seo: {
                title: xmlProduct.name,
                description: this.truncateDescription(xmlProduct.description, 160)
            }
        };
    }
    
    generateSlug(name) {
        return name.toLowerCase()
            .replace(/ş/g, 's').replace(/ğ/g, 'g').replace(/ü/g, 'u')
            .replace(/ö/g, 'o').replace(/ç/g, 'c').replace(/ı/g, 'i')
            .replace(/\s+/g, '-')
            .replace(/\//g, '-');
    }
    
    generateCategoryPath(categoryName) {
        return '/' + this.generateSlug(categoryName);
    }
    
    htmlDecode(html) {
        const entities = {
            '&amp;': '&',
            '&lt;': '<',
            '&gt;': '>',
            '&quot;': '"',
            '&#39;': "'"
        };
        return html.replace(/&[^;]+;/g, entity => entities[entity] || entity);
    }
    
    truncateDescription(text, maxLength) {
        if (!text || text.length <= maxLength) return text;
        return text.substring(0, maxLength) + '...';
    }
    
    mapImages(imageUrls) {
        if (!imageUrls || imageUrls.length === 0) return [];
        
        return imageUrls.map((url, index) => ({
            src: url,
            position: index + 1
        }));
    }
}

module.exports = new IkasFieldMapper();
```

### İkas API Authentication

İkas API için **sadece 2 bilgi** yeterli:

1. **API Key** (Username gibi)
2. **API Secret** (Password gibi)

API endpoint sabit: `https://api.myikas.com`

**Örnek Request:**
```http
POST /api/v1/products HTTP/1.1
Host: api.myikas.com
Content-Type: application/json
Authorization: Bearer sk_live_abc123:secret_xyz789

{
  "name": "Şeffaf Epoksi Reçine 1kg",
  "sku": "REC-EPO-001",
  ...
}
```

### İkas API Response

**Başarılı Response (201 Created):**
```json
{
  "id": "prod_abc123xyz",
  "name": "Şeffaf Epoksi Reçine 1kg",
  "sku": "REC-EPO-001",
  "status": "active",
  "createdAt": "2026-05-14T10:30:00Z",
  "updatedAt": "2026-05-14T10:30:00Z"
}
```

**Hatalı Response (400 Bad Request):**
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Validation failed",
    "details": [
      {
        "field": "sku",
        "message": "SKU already exists"
      }
    ]
  }
}
```

---

## 🏗️ Mimari Tasarım

```
┌─────────────────────────────────────────────────────────────┐
│                    hobizubi.com (Master)                     │
│                         XML Export                           │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              Backend API Service                             │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Controllers (REST API)                             │    │
│  │    ├─ XMLController                                 │    │
│  │    ├─ SiteMappingController                         │    │
│  │    ├─ TransferController                            │    │
│  │    └─ DashboardController                           │    │
│  └──────────────────┬──────────────────────────────────┘    │
│  ┌──────────────────▼──────────────────────────────────┐    │
│  │  Business Logic Layer                               │    │
│  │    ├─ XMLService                                    │    │
│  │    ├─ CategoryFilterService                         │    │
│  │    ├─ PricingService                                │    │
│  │    ├─ IkasApiService                                │    │
│  │    └─ TransferLogService                            │    │
│  └──────────────────┬──────────────────────────────────┘    │
│  ┌──────────────────▼──────────────────────────────────┐    │
│  │  Data Access Layer                                  │    │
│  │    ├─ SiteMappingRepository                         │    │
│  │    ├─ ProductRepository                             │    │
│  │    ├─ TransferLogRepository                         │    │
│  │    └─ IkasApiClient                                 │    │
│  └──────────────────┬──────────────────────────────────┘    │
└────────────────────┬┴───────────────────────────────────────┘
                     │
         ┌───────────┴───────────┐
         ▼                       ▼
┌────────────────┐      ┌────────────────┐
│   SQL Server   │      │  İkas API      │
│   Database     │      │  Integration   │
└────────────────┘      └────────────────┘

┌─────────────────────────────────────────────────────────────┐
│              Frontend Dashboard (React/Vue)                  │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  - Site Mapping Configuration                       │    │
│  │  - Transfer Status Monitoring                       │    │
│  │  - Statistics & Reports                             │    │
│  │  - Manual Transfer Trigger                          │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│              Background Jobs (Scheduler)                     │
│  - Daily XML Sync Job                                        │
│  - Error Notification Job                                    │
│  - Health Check Job                                          │
└─────────────────────────────────────────────────────────────┘
```

---

## 💾 Veritabanı Tasarımı

### Tablo Yapısı

#### 1. `companies` (Mevcut Tablo - Referans)
```sql
CREATE TABLE [dbo].[companies] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [name] NVARCHAR(255) NOT NULL,
    [email] NVARCHAR(255),
    [is_active] BIT DEFAULT 1,
    [created_date] DATETIME DEFAULT GETDATE()
);
```

#### 2. `xml_sources` (XML Kaynak Tanımları)
```sql
CREATE TABLE [dbo].[xml_sources] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [name] NVARCHAR(255) NOT NULL,
    [source_company_id] BIGINT NOT NULL,
    [xml_url] NVARCHAR(1000) NOT NULL,
    [is_active] BIT DEFAULT 1,
    [sync_frequency_hours] INT DEFAULT 24,
    [last_sync_date] DATETIME,
    [next_sync_date] DATETIME,
    [created_date] DATETIME DEFAULT GETDATE(),
    [updated_date] DATETIME,
    
    CONSTRAINT FK_XmlSource_Company 
        FOREIGN KEY ([source_company_id]) 
        REFERENCES [companies]([id])
);

CREATE INDEX IX_XmlSources_SourceCompany ON [xml_sources]([source_company_id]);
CREATE INDEX IX_XmlSources_NextSync ON [xml_sources]([next_sync_date]);
```

#### 3. `site_mappings` (Site Konfigürasyonları)

> **ÖNEMLİ:** İkas API için sadece `ikas_api_key` ve `ikas_api_secret` yeterlidir. API endpoint sabit: `https://api.myikas.com`

```sql
CREATE TABLE [dbo].[site_mappings] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [xml_source_id] BIGINT NOT NULL,
    [target_company_id] BIGINT NOT NULL,
    
    -- İkas API Credentials (Sadece bunlar yeterli!)
    [ikas_api_key] NVARCHAR(500) NOT NULL,      -- Örnek: sk_live_abc123
    [ikas_api_secret] NVARCHAR(500) NOT NULL,   -- Örnek: secret_xyz789
    
    -- Pricing
    [price_margin_percentage] DECIMAL(18,2) DEFAULT 0,
    [additional_price] DECIMAL(18,2) DEFAULT 0,
    
    -- Filters & Mappings (JSON)
    [category_filters] NVARCHAR(MAX),           -- JSON array: ["Reçine", "Boncuk > *"]
    [category_mappings] NVARCHAR(MAX),          -- JSON object: {"Reçine": "Resin"}
    
    -- Status
    [is_active] BIT DEFAULT 1,
    [created_date] DATETIME DEFAULT GETDATE(),
    [updated_date] DATETIME,
    
    CONSTRAINT FK_SiteMapping_XmlSource 
        FOREIGN KEY ([xml_source_id]) 
        REFERENCES [xml_sources]([id]),
    CONSTRAINT FK_SiteMapping_TargetCompany 
        FOREIGN KEY ([target_company_id]) 
        REFERENCES [companies]([id])
);

CREATE INDEX IX_SiteMapping_XmlSource ON [site_mappings]([xml_source_id]);
CREATE INDEX IX_SiteMapping_TargetCompany ON [site_mappings]([target_company_id]);
```

#### 4. `products` (Ürün Ana Tablosu)
```sql
CREATE TABLE [dbo].[products] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [company_id] BIGINT NOT NULL,
    [xml_source_id] BIGINT,
    
    -- Product Details
    [sku] NVARCHAR(255) NOT NULL,
    [barcode] NVARCHAR(255),
    [name] NVARCHAR(1000) NOT NULL,
    [description] NVARCHAR(MAX),
    [category_path] NVARCHAR(500),
    [brand] NVARCHAR(255),
    
    -- Pricing
    [original_price] DECIMAL(18,2) NOT NULL,
    [sale_price] DECIMAL(18,2) NOT NULL,
    [discount_price] DECIMAL(18,2),
    [currency] NVARCHAR(10) DEFAULT 'TRY',
    
    -- Inventory
    [stock_quantity] INT DEFAULT 0,
    [weight] DECIMAL(18,2),
    
    -- Status
    [is_active] BIT DEFAULT 1,
    [created_date] DATETIME DEFAULT GETDATE(),
    [updated_date] DATETIME,
    
    CONSTRAINT FK_Product_Company 
        FOREIGN KEY ([company_id]) 
        REFERENCES [companies]([id]),
    CONSTRAINT FK_Product_XmlSource 
        FOREIGN KEY ([xml_source_id]) 
        REFERENCES [xml_sources]([id])
);

CREATE INDEX IX_Products_Company ON [products]([company_id]);
CREATE INDEX IX_Products_Sku ON [products]([sku]);
CREATE INDEX IX_Products_XmlSource ON [products]([xml_source_id]);
```

#### 5. `product_transfers` (Transfer İlişkileri)
```sql
CREATE TABLE [dbo].[product_transfers] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [source_product_id] BIGINT NOT NULL,
    [target_company_id] BIGINT NOT NULL,
    [site_mapping_id] BIGINT NOT NULL,
    
    -- İkas Integration
    [ikas_product_id] NVARCHAR(255),
    [ikas_variant_id] NVARCHAR(255),
    
    -- Transfer Details
    [transferred_price] DECIMAL(18,2),
    [transferred_category] NVARCHAR(500),
    [transfer_status] INT DEFAULT 0,        -- 0=Pending, 1=Success, 2=Failed
    [error_message] NVARCHAR(MAX),
    
    -- Dates
    [first_transfer_date] DATETIME,
    [last_transfer_date] DATETIME,
    [created_date] DATETIME DEFAULT GETDATE(),
    
    CONSTRAINT FK_ProductTransfer_SourceProduct 
        FOREIGN KEY ([source_product_id]) 
        REFERENCES [products]([id]),
    CONSTRAINT FK_ProductTransfer_TargetCompany 
        FOREIGN KEY ([target_company_id]) 
        REFERENCES [companies]([id]),
    CONSTRAINT FK_ProductTransfer_SiteMapping 
        FOREIGN KEY ([site_mapping_id]) 
        REFERENCES [site_mappings]([id])
);

CREATE INDEX IX_ProductTransfers_SourceProduct ON [product_transfers]([source_product_id]);
CREATE INDEX IX_ProductTransfers_TargetCompany ON [product_transfers]([target_company_id]);
CREATE INDEX IX_ProductTransfers_Status ON [product_transfers]([transfer_status]);
```

#### 6. `transfer_logs` (Transfer İşlem Logları)
```sql
CREATE TABLE [dbo].[transfer_logs] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [xml_source_id] BIGINT NOT NULL,
    [site_mapping_id] BIGINT NOT NULL,
    [target_company_id] BIGINT NOT NULL,
    
    -- Transfer Stats
    [total_products_in_xml] INT DEFAULT 0,
    [filtered_products_count] INT DEFAULT 0,
    [success_count] INT DEFAULT 0,
    [error_count] INT DEFAULT 0,
    [skipped_count] INT DEFAULT 0,
    
    -- Timing
    [start_date] DATETIME NOT NULL,
    [end_date] DATETIME,
    [duration_seconds] INT,
    
    -- Status & Errors
    [status] INT DEFAULT 1,                 -- 1=InProgress, 2=Success, 3=Failed
    [error_details] NVARCHAR(MAX),          -- JSON array of errors
    
    CONSTRAINT FK_TransferLog_XmlSource 
        FOREIGN KEY ([xml_source_id]) 
        REFERENCES [xml_sources]([id]),
    CONSTRAINT FK_TransferLog_SiteMapping 
        FOREIGN KEY ([site_mapping_id]) 
        REFERENCES [site_mappings]([id])
);

CREATE INDEX IX_TransferLogs_XmlSource ON [transfer_logs]([xml_source_id]);
CREATE INDEX IX_TransferLogs_StartDate ON [transfer_logs]([start_date]);
CREATE INDEX IX_TransferLogs_Status ON [transfer_logs]([status]);
```

### Veritabanı Kurulum SQL Script

```sql
-- Full Database Setup Script
-- Run this in SQL Server Management Studio

USE [MultiSiteIkasDB]
GO

-- 1. Companies Table (if not exists)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'companies')
BEGIN
    CREATE TABLE [dbo].[companies] (
        [id] BIGINT PRIMARY KEY IDENTITY(1,1),
        [name] NVARCHAR(255) NOT NULL,
        [email] NVARCHAR(255),
        [is_active] BIT DEFAULT 1,
        [created_date] DATETIME DEFAULT GETDATE()
    );
END
GO

-- 2. XML Sources Table
CREATE TABLE [dbo].[xml_sources] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [name] NVARCHAR(255) NOT NULL,
    [source_company_id] BIGINT NOT NULL,
    [xml_url] NVARCHAR(1000) NOT NULL,
    [is_active] BIT DEFAULT 1,
    [sync_frequency_hours] INT DEFAULT 24,
    [last_sync_date] DATETIME,
    [next_sync_date] DATETIME,
    [created_date] DATETIME DEFAULT GETDATE(),
    [updated_date] DATETIME,
    CONSTRAINT FK_XmlSource_Company FOREIGN KEY ([source_company_id]) REFERENCES [companies]([id])
);
CREATE INDEX IX_XmlSources_SourceCompany ON [xml_sources]([source_company_id]);
CREATE INDEX IX_XmlSources_NextSync ON [xml_sources]([next_sync_date]);
GO

-- 3. Site Mappings Table
CREATE TABLE [dbo].[site_mappings] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [xml_source_id] BIGINT NOT NULL,
    [target_company_id] BIGINT NOT NULL,
    [ikas_api_key] NVARCHAR(500) NOT NULL,
    [ikas_api_secret] NVARCHAR(500) NOT NULL,
    [price_margin_percentage] DECIMAL(18,2) DEFAULT 0,
    [additional_price] DECIMAL(18,2) DEFAULT 0,
    [category_filters] NVARCHAR(MAX),
    [category_mappings] NVARCHAR(MAX),
    [is_active] BIT DEFAULT 1,
    [created_date] DATETIME DEFAULT GETDATE(),
    [updated_date] DATETIME,
    CONSTRAINT FK_SiteMapping_XmlSource FOREIGN KEY ([xml_source_id]) REFERENCES [xml_sources]([id]),
    CONSTRAINT FK_SiteMapping_TargetCompany FOREIGN KEY ([target_company_id]) REFERENCES [companies]([id])
);
CREATE INDEX IX_SiteMapping_XmlSource ON [site_mappings]([xml_source_id]);
CREATE INDEX IX_SiteMapping_TargetCompany ON [site_mappings]([target_company_id]);
GO

-- 4. Products Table
CREATE TABLE [dbo].[products] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [company_id] BIGINT NOT NULL,
    [xml_source_id] BIGINT,
    [sku] NVARCHAR(255) NOT NULL,
    [barcode] NVARCHAR(255),
    [name] NVARCHAR(1000) NOT NULL,
    [description] NVARCHAR(MAX),
    [category_path] NVARCHAR(500),
    [brand] NVARCHAR(255),
    [original_price] DECIMAL(18,2) NOT NULL,
    [sale_price] DECIMAL(18,2) NOT NULL,
    [discount_price] DECIMAL(18,2),
    [currency] NVARCHAR(10) DEFAULT 'TRY',
    [stock_quantity] INT DEFAULT 0,
    [weight] DECIMAL(18,2),
    [is_active] BIT DEFAULT 1,
    [created_date] DATETIME DEFAULT GETDATE(),
    [updated_date] DATETIME,
    CONSTRAINT FK_Product_Company FOREIGN KEY ([company_id]) REFERENCES [companies]([id]),
    CONSTRAINT FK_Product_XmlSource FOREIGN KEY ([xml_source_id]) REFERENCES [xml_sources]([id])
);
CREATE INDEX IX_Products_Company ON [products]([company_id]);
CREATE INDEX IX_Products_Sku ON [products]([sku]);
CREATE INDEX IX_Products_XmlSource ON [products]([xml_source_id]);
GO

-- 5. Product Transfers Table
CREATE TABLE [dbo].[product_transfers] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [source_product_id] BIGINT NOT NULL,
    [target_company_id] BIGINT NOT NULL,
    [site_mapping_id] BIGINT NOT NULL,
    [ikas_product_id] NVARCHAR(255),
    [ikas_variant_id] NVARCHAR(255),
    [transferred_price] DECIMAL(18,2),
    [transferred_category] NVARCHAR(500),
    [transfer_status] INT DEFAULT 0,
    [error_message] NVARCHAR(MAX),
    [first_transfer_date] DATETIME,
    [last_transfer_date] DATETIME,
    [created_date] DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_ProductTransfer_SourceProduct FOREIGN KEY ([source_product_id]) REFERENCES [products]([id]),
    CONSTRAINT FK_ProductTransfer_TargetCompany FOREIGN KEY ([target_company_id]) REFERENCES [companies]([id]),
    CONSTRAINT FK_ProductTransfer_SiteMapping FOREIGN KEY ([site_mapping_id]) REFERENCES [site_mappings]([id])
);
CREATE INDEX IX_ProductTransfers_SourceProduct ON [product_transfers]([source_product_id]);
CREATE INDEX IX_ProductTransfers_TargetCompany ON [product_transfers]([target_company_id]);
CREATE INDEX IX_ProductTransfers_Status ON [product_transfers]([transfer_status]);
GO

-- 6. Transfer Logs Table
CREATE TABLE [dbo].[transfer_logs] (
    [id] BIGINT PRIMARY KEY IDENTITY(1,1),
    [xml_source_id] BIGINT NOT NULL,
    [site_mapping_id] BIGINT NOT NULL,
    [target_company_id] BIGINT NOT NULL,
    [total_products_in_xml] INT DEFAULT 0,
    [filtered_products_count] INT DEFAULT 0,
    [success_count] INT DEFAULT 0,
    [error_count] INT DEFAULT 0,
    [skipped_count] INT DEFAULT 0,
    [start_date] DATETIME NOT NULL,
    [end_date] DATETIME,
    [duration_seconds] INT,
    [status] INT DEFAULT 1,
    [error_details] NVARCHAR(MAX),
    CONSTRAINT FK_TransferLog_XmlSource FOREIGN KEY ([xml_source_id]) REFERENCES [xml_sources]([id]),
    CONSTRAINT FK_TransferLog_SiteMapping FOREIGN KEY ([site_mapping_id]) REFERENCES [site_mappings]([id])
);
CREATE INDEX IX_TransferLogs_XmlSource ON [transfer_logs]([xml_source_id]);
CREATE INDEX IX_TransferLogs_StartDate ON [transfer_logs]([start_date]);
CREATE INDEX IX_TransferLogs_Status ON [transfer_logs]([status]);
GO

-- Sample Data
INSERT INTO [companies] ([name], [email]) VALUES 
('Hobi Zubi', 'admin@hobizubi.com'),
('Reçinem', 'admin@recinem.com'),
('Boncuk Pasajı', 'admin@boncukpasaji.com'),
('Kalıp Atölyesi', 'admin@kalipatolyesi.com'),
('Mall of Molds', 'admin@mallofmolds.com');

INSERT INTO [xml_sources] ([name], [source_company_id], [xml_url], [sync_frequency_hours]) VALUES 
('Hobi Zubi XML Feed', 1, 'https://hobizubi.com/export/products.xml', 24);

INSERT INTO [site_mappings] 
([xml_source_id], [target_company_id], [price_margin_percentage], [category_filters], [category_mappings]) 
VALUES 
(1, 2, 15.00, '["Reçine", "Epoksi", "Pigment"]', '{"Reçine": "Resin Products"}'),
(1, 3, 20.00, '["Boncuk", "İp ve Tel", "Takı Malzemeleri"]', '{}'),
(1, 4, 18.00, '["Silikon Kalıp", "Reçine Kalıp"]', '{}'),
(1, 5, 25.00, '["Silikon Kalıp", "Reçine Kalıp"]', '{"Silikon Kalıp": "Silicone Molds", "Reçine Kalıp": "Resin Molds"}');
GO

PRINT 'Database setup completed successfully!'
```

---

## 🔷 C# Implementation

### Proje Yapısı

```
MultiSiteIkas.Solution/
├── MultiSiteIkas.API/                  # ASP.NET Core Web API
│   ├── Controllers/
│   │   ├── XmlSourceController.cs
│   │   ├── SiteMappingController.cs
│   │   ├── TransferController.cs
│   │   └── DashboardController.cs
│   ├── Program.cs
│   └── appsettings.json
│
├── MultiSiteIkas.Core/                 # Business Logic
│   ├── Services/
│   │   ├── XmlParsingService.cs
│   │   ├── CategoryFilterService.cs
│   │   ├── PricingService.cs
│   │   ├── IkasApiService.cs
│   │   └── TransferService.cs
│   ├── Models/
│   │   ├── Product.cs
│   │   ├── SiteMapping.cs
│   │   └── TransferLog.cs
│   └── Interfaces/
│       └── IServices...
│
├── MultiSiteIkas.Data/                 # Data Access Layer
│   ├── Repositories/
│   │   ├── XmlSourceRepository.cs
│   │   ├── SiteMappingRepository.cs
│   │   ├── ProductRepository.cs
│   │   └── TransferLogRepository.cs
│   ├── DbContext/
│   │   └── MultiSiteDbContext.cs
│   └── Interfaces/
│       └── IRepositories...
│
├── MultiSiteIkas.Jobs/                 # Background Jobs
│   ├── Jobs/
│   │   ├── DailyXmlSyncJob.cs
│   │   └── HealthCheckJob.cs
│   └── Program.cs
│
└── MultiSiteIkas.Tests/                # Unit Tests
    ├── ServiceTests/
    └── IntegrationTests/
```

### Adım 1: Solution ve Projeler Oluştur

```bash
# 1. Solution oluştur
dotnet new sln -n MultiSiteIkas

# 2. Web API Projesi
dotnet new webapi -n MultiSiteIkas.API
dotnet sln add MultiSiteIkas.API/MultiSiteIkas.API.csproj

# 3. Core (Business Logic) Projesi
dotnet new classlib -n MultiSiteIkas.Core
dotnet sln add MultiSiteIkas.Core/MultiSiteIkas.Core.csproj

# 4. Data Access Projesi
dotnet new classlib -n MultiSiteIkas.Data
dotnet sln add MultiSiteIkas.Data/MultiSiteIkas.Data.csproj

# 5. Jobs (Background Worker) Projesi
dotnet new worker -n MultiSiteIkas.Jobs
dotnet sln add MultiSiteIkas.Jobs/MultiSiteIkas.Jobs.csproj

# 6. Test Projesi
dotnet new xunit -n MultiSiteIkas.Tests
dotnet sln add MultiSiteIkas.Tests/MultiSiteIkas.Tests.csproj

# 7. Proje referansları ekle
dotnet add MultiSiteIkas.API/MultiSiteIkas.API.csproj reference MultiSiteIkas.Core/MultiSiteIkas.Core.csproj
dotnet add MultiSiteIkas.API/MultiSiteIkas.API.csproj reference MultiSiteIkas.Data/MultiSiteIkas.Data.csproj
dotnet add MultiSiteIkas.Core/MultiSiteIkas.Core.csproj reference MultiSiteIkas.Data/MultiSiteIkas.Data.csproj
dotnet add MultiSiteIkas.Jobs/MultiSiteIkas.Jobs.csproj reference MultiSiteIkas.Core/MultiSiteIkas.Core.csproj
dotnet add MultiSiteIkas.Tests/MultiSiteIkas.Tests.csproj reference MultiSiteIkas.Core/MultiSiteIkas.Core.csproj
```

### Adım 2: NuGet Paketlerini Yükle

```bash
# API Project
cd MultiSiteIkas.API
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Swashbuckle.AspNetCore
dotnet add package Serilog.AspNetCore
dotnet add package FluentValidation.AspNetCore

# Core Project
cd ../MultiSiteIkas.Core
dotnet add package Newtonsoft.Json
dotnet add package System.Xml.XmlDocument

# Data Project
cd ../MultiSiteIkas.Data
dotnet add package Dapper
dotnet add package Microsoft.EntityFrameworkCore.SqlServer

# Jobs Project
cd ../MultiSiteIkas.Jobs
dotnet add package Hangfire
dotnet add package Hangfire.SqlServer
dotnet add package Hangfire.AspNetCore
```

### Adım 3: Data Layer - Models

**MultiSiteIkas.Core/Models/Product.cs**
```csharp
using System;

namespace MultiSiteIkas.Core.Models
{
    public class Product
    {
        public long Id { get; set; }
        public long CompanyId { get; set; }
        public long? XmlSourceId { get; set; }
        
        // Product Details
        public string Sku { get; set; }
        public string Barcode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string CategoryPath { get; set; }
        public string Brand { get; set; }
        
        // Pricing
        public decimal OriginalPrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal? DiscountPrice { get; set; }
        public string Currency { get; set; } = "TRY";
        
        // Inventory
        public int StockQuantity { get; set; }
        public decimal? Weight { get; set; }
        
        // Status
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }
        
        // Navigation
        public Company Company { get; set; }
        public XmlSource XmlSource { get; set; }
    }
}
```

**MultiSiteIkas.Core/Models/SiteMapping.cs**
```csharp
using System;
using System.Collections.Generic;

namespace MultiSiteIkas.Core.Models
{
    public class SiteMapping
    {
        public long Id { get; set; }
        public long XmlSourceId { get; set; }
        public long TargetCompanyId { get; set; }
        
        // İkas Credentials (API endpoint sabit: https://api.myikas.com)
        public string IkasApiKey { get; set; }
        public string IkasApiSecret { get; set; }
        
        // Pricing
        public decimal PriceMarginPercentage { get; set; }
        public decimal AdditionalPrice { get; set; }
        
        // Filters & Mappings (stored as JSON)
        public string CategoryFilters { get; set; }
        public string CategoryMappings { get; set; }
        
        // Status
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }
        
        // Parsed Properties (not stored in DB)
        public List<string> CategoryFilterList { get; set; }
        public Dictionary<string, string> CategoryMappingDict { get; set; }
        
        // Navigation
        public XmlSource XmlSource { get; set; }
        public Company TargetCompany { get; set; }
    }
}
```

**MultiSiteIkas.Core/Models/XmlSource.cs**
```csharp
using System;

namespace MultiSiteIkas.Core.Models
{
    public class XmlSource
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long SourceCompanyId { get; set; }
        public string XmlUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public int SyncFrequencyHours { get; set; } = 24;
        public DateTime? LastSyncDate { get; set; }
        public DateTime? NextSyncDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }
        
        // Navigation
        public Company SourceCompany { get; set; }
    }
}
```

**MultiSiteIkas.Core/Models/TransferLog.cs**
```csharp
using System;
using System.Collections.Generic;

namespace MultiSiteIkas.Core.Models
{
    public class TransferLog
    {
        public long Id { get; set; }
        public long XmlSourceId { get; set; }
        public long SiteMappingId { get; set; }
        public long TargetCompanyId { get; set; }
        
        // Transfer Stats
        public int TotalProductsInXml { get; set; }
        public int FilteredProductsCount { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public int SkippedCount { get; set; }
        
        // Timing
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? DurationSeconds { get; set; }
        
        // Status
        public TransferStatus Status { get; set; } = TransferStatus.InProgress;
        public string ErrorDetails { get; set; }
        
        // Parsed Errors
        public List<TransferError> Errors { get; set; }
    }
    
    public enum TransferStatus
    {
        InProgress = 1,
        Success = 2,
        Failed = 3
    }
    
    public class TransferError
    {
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime ErrorDate { get; set; }
    }
}
```

**MultiSiteIkas.Core/Models/Company.cs**
```csharp
using System;

namespace MultiSiteIkas.Core.Models
{
    public class Company
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
```

### Adım 4: Data Layer - Repositories

**MultiSiteIkas.Data/Interfaces/IRepository.cs**
```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MultiSiteIkas.Data.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T> GetByIdAsync(long id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<long> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(long id);
    }
}
```

**MultiSiteIkas.Data/Repositories/SiteMappingRepository.cs**
```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MultiSiteIkas.Core.Models;
using Newtonsoft.Json;

namespace MultiSiteIkas.Data.Repositories
{
    public interface ISiteMappingRepository
    {
        Task<SiteMapping> GetByIdAsync(long id);
        Task<IEnumerable<SiteMapping>> GetByXmlSourceIdAsync(long xmlSourceId);
        Task<IEnumerable<SiteMapping>> GetActiveByXmlSourceIdAsync(long xmlSourceId);
        Task<long> AddAsync(SiteMapping mapping);
        Task UpdateAsync(SiteMapping mapping);
        Task DeleteAsync(long id);
    }
    
    public class SiteMappingRepository : ISiteMappingRepository
    {
        private readonly string _connectionString;
        
        public SiteMappingRepository(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        public async Task<SiteMapping> GetByIdAsync(long id)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT * FROM site_mappings 
                WHERE id = @Id";
            
            var mapping = await conn.QueryFirstOrDefaultAsync<SiteMapping>(sql, new { Id = id });
            
            if (mapping != null)
            {
                ParseJsonFields(mapping);
            }
            
            return mapping;
        }
        
        public async Task<IEnumerable<SiteMapping>> GetByXmlSourceIdAsync(long xmlSourceId)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT * FROM site_mappings 
                WHERE xml_source_id = @XmlSourceId";
            
            var mappings = await conn.QueryAsync<SiteMapping>(sql, new { XmlSourceId = xmlSourceId });
            
            foreach (var mapping in mappings)
            {
                ParseJsonFields(mapping);
            }
            
            return mappings;
        }
        
        public async Task<IEnumerable<SiteMapping>> GetActiveByXmlSourceIdAsync(long xmlSourceId)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT * FROM site_mappings 
                WHERE xml_source_id = @XmlSourceId AND is_active = 1";
            
            var mappings = await conn.QueryAsync<SiteMapping>(sql, new { XmlSourceId = xmlSourceId });
            
            foreach (var mapping in mappings)
            {
                ParseJsonFields(mapping);
            }
            
            return mappings;
        }
        
        public async Task<long> AddAsync(SiteMapping mapping)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                INSERT INTO site_mappings 
                (xml_source_id, target_company_id, ikas_api_key, ikas_api_secret, ikas_store_url,
                 price_margin_percentage, additional_price, category_filters, category_mappings, 
                 is_active, created_date)
                VALUES 
                (@XmlSourceId, @TargetCompanyId, @IkasApiKey, @IkasApiSecret,
                 @PriceMarginPercentage, @AdditionalPrice, @CategoryFilters, @CategoryMappings,
                 @IsActive, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() as bigint)";
            
            return await conn.ExecuteScalarAsync<long>(sql, mapping);
        }
        
        public async Task UpdateAsync(SiteMapping mapping)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                UPDATE site_mappings 
                SET ikas_api_key = @IkasApiKey,
                    ikas_api_secret = @IkasApiSecret,
                    price_margin_percentage = @PriceMarginPercentage,
                    additional_price = @AdditionalPrice,
                    category_filters = @CategoryFilters,
                    category_mappings = @CategoryMappings,
                    is_active = @IsActive,
                    updated_date = GETDATE()
                WHERE id = @Id";
            
            await conn.ExecuteAsync(sql, mapping);
        }
        
        public async Task DeleteAsync(long id)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = "DELETE FROM site_mappings WHERE id = @Id";
            await conn.ExecuteAsync(sql, new { Id = id });
        }
        
        private void ParseJsonFields(SiteMapping mapping)
        {
            if (!string.IsNullOrEmpty(mapping.CategoryFilters))
            {
                mapping.CategoryFilterList = JsonConvert.DeserializeObject<List<string>>(mapping.CategoryFilters);
            }
            
            if (!string.IsNullOrEmpty(mapping.CategoryMappings))
            {
                mapping.CategoryMappingDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(mapping.CategoryMappings);
            }
        }
    }
}
```

**MultiSiteIkas.Data/Repositories/TransferLogRepository.cs**
```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MultiSiteIkas.Core.Models;
using Newtonsoft.Json;

namespace MultiSiteIkas.Data.Repositories
{
    public interface ITransferLogRepository
    {
        Task<long> StartLogAsync(TransferLog log);
        Task CompleteLogAsync(long logId, TransferLog log);
        Task<IEnumerable<TransferLog>> GetRecentLogsAsync(long xmlSourceId, int count = 50);
        Task<IEnumerable<TransferLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate);
    }
    
    public class TransferLogRepository : ITransferLogRepository
    {
        private readonly string _connectionString;
        
        public TransferLogRepository(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        public async Task<long> StartLogAsync(TransferLog log)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                INSERT INTO transfer_logs 
                (xml_source_id, site_mapping_id, target_company_id, total_products_in_xml,
                 start_date, status)
                VALUES 
                (@XmlSourceId, @SiteMappingId, @TargetCompanyId, @TotalProductsInXml,
                 GETDATE(), 1);
                SELECT CAST(SCOPE_IDENTITY() as bigint)";
            
            return await conn.ExecuteScalarAsync<long>(sql, log);
        }
        
        public async Task CompleteLogAsync(long logId, TransferLog log)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                UPDATE transfer_logs 
                SET end_date = GETDATE(),
                    duration_seconds = DATEDIFF(SECOND, start_date, GETDATE()),
                    filtered_products_count = @FilteredProductsCount,
                    success_count = @SuccessCount,
                    error_count = @ErrorCount,
                    skipped_count = @SkippedCount,
                    error_details = @ErrorDetails,
                    status = @Status
                WHERE id = @Id";
            
            await conn.ExecuteAsync(sql, new
            {
                Id = logId,
                log.FilteredProductsCount,
                log.SuccessCount,
                log.ErrorCount,
                log.SkippedCount,
                log.ErrorDetails,
                Status = (int)log.Status
            });
        }
        
        public async Task<IEnumerable<TransferLog>> GetRecentLogsAsync(long xmlSourceId, int count = 50)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT TOP (@Count) * 
                FROM transfer_logs 
                WHERE xml_source_id = @XmlSourceId
                ORDER BY start_date DESC";
            
            var logs = await conn.QueryAsync<TransferLog>(sql, new { XmlSourceId = xmlSourceId, Count = count });
            
            foreach (var log in logs)
            {
                if (!string.IsNullOrEmpty(log.ErrorDetails))
                {
                    log.Errors = JsonConvert.DeserializeObject<List<TransferError>>(log.ErrorDetails);
                }
            }
            
            return logs;
        }
        
        public async Task<IEnumerable<TransferLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT * FROM transfer_logs 
                WHERE start_date >= @StartDate AND start_date <= @EndDate
                ORDER BY start_date DESC";
            
            return await conn.QueryAsync<TransferLog>(sql, new { StartDate = startDate, EndDate = endDate });
        }
    }
}
```

### Adım 5: Business Logic Layer - Services

**MultiSiteIkas.Core/Services/CategoryFilterService.cs**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using MultiSiteIkas.Core.Models;

namespace MultiSiteIkas.Core.Services
{
    public interface ICategoryFilterService
    {
        bool IsProductAllowedForSite(string productCategory, SiteMapping mapping);
        string MapCategory(string sourceCategory, SiteMapping mapping);
    }
    
    public class CategoryFilterService : ICategoryFilterService
    {
        public bool IsProductAllowedForSite(string productCategory, SiteMapping mapping)
        {
            if (mapping.CategoryFilterList == null || mapping.CategoryFilterList.Count == 0)
                return true; // No filter = allow all
            
            foreach (var filter in mapping.CategoryFilterList)
            {
                // Wildcard support: "Boncuk > *"
                if (filter.Contains(" > *") || filter.Contains(">*"))
                {
                    var parentCategory = filter.Replace(" > *", "").Replace(">*", "").Trim();
                    if (productCategory.StartsWith(parentCategory, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                // Exact or partial match
                else if (productCategory.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        public string MapCategory(string sourceCategory, SiteMapping mapping)
        {
            if (mapping.CategoryMappingDict == null || mapping.CategoryMappingDict.Count == 0)
                return sourceCategory;
            
            // Split by " > " and map each level
            var categories = sourceCategory.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
            var mappedCategories = new List<string>();
            
            foreach (var cat in categories)
            {
                var trimmedCat = cat.Trim();
                if (mapping.CategoryMappingDict.ContainsKey(trimmedCat))
                    mappedCategories.Add(mapping.CategoryMappingDict[trimmedCat]);
                else
                    mappedCategories.Add(trimmedCat);
            }
            
            return string.Join(" > ", mappedCategories);
        }
    }
}
```

**MultiSiteIkas.Core/Services/PricingService.cs**
```csharp
using System;
using MultiSiteIkas.Core.Models;

namespace MultiSiteIkas.Core.Services
{
    public interface IPricingService
    {
        decimal CalculateSitePrice(decimal originalPrice, SiteMapping mapping);
    }
    
    public class PricingService : IPricingService
    {
        public decimal CalculateSitePrice(decimal originalPrice, SiteMapping mapping)
        {
            // Step 1: Apply percentage margin
            var priceWithMargin = originalPrice * (1 + (mapping.PriceMarginPercentage / 100));
            
            // Step 2: Add fixed price
            var finalPrice = priceWithMargin + mapping.AdditionalPrice;
            
            // Step 3: Round to 2 decimals
            return Math.Round(finalPrice, 2);
        }
    }
}
```

**MultiSiteIkas.Core/Services/XmlParsingService.cs**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using MultiSiteIkas.Core.Models;

namespace MultiSiteIkas.Core.Services
{
    public interface IXmlParsingService
    {
        Task<List<Product>> ParseXmlAsync(string xmlUrl, long xmlSourceId);
    }
    
    public class XmlParsingService : IXmlParsingService
    {
        private readonly HttpClient _httpClient;
        
        public XmlParsingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        
        public async Task<List<Product>> ParseXmlAsync(string xmlUrl, long xmlSourceId)
        {
            var products = new List<Product>();
            
            try
            {
                // Download XML
                var xmlContent = await _httpClient.GetStringAsync(xmlUrl);
                var xDoc = XDocument.Parse(xmlContent);
                
                // Parse products (adjust based on your XML structure)
                var productElements = xDoc.Descendants("product");
                
                foreach (var element in productElements)
                {
                    var product = new Product
                    {
                        XmlSourceId = xmlSourceId,
                        Sku = element.Element("sku")?.Value,
                        Barcode = element.Element("barcode")?.Value,
                        Name = element.Element("name")?.Value,
                        Description = element.Element("description")?.Value,
                        CategoryPath = element.Element("category")?.Value,
                        Brand = element.Element("brand")?.Value,
                        OriginalPrice = decimal.Parse(element.Element("price")?.Value ?? "0"),
                        SalePrice = decimal.Parse(element.Element("sale_price")?.Value ?? element.Element("price")?.Value ?? "0"),
                        DiscountPrice = element.Element("discount_price") != null 
                            ? decimal.Parse(element.Element("discount_price").Value) 
                            : (decimal?)null,
                        StockQuantity = int.Parse(element.Element("stock")?.Value ?? "0"),
                        Weight = element.Element("weight") != null 
                            ? decimal.Parse(element.Element("weight").Value) 
                            : (decimal?)null,
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    };
                    
                    products.Add(product);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"XML parsing error: {ex.Message}", ex);
            }
            
            return products;
        }
    }
}
```

**MultiSiteIkas.Core/Services/IkasApiService.cs**
```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MultiSiteIkas.Core.Models;
using Newtonsoft.Json;

namespace MultiSiteIkas.Core.Services
{
    public interface IIkasApiService
    {
        Task<IkasApiResponse> CreateOrUpdateProductAsync(Product product, SiteMapping mapping, string ikasProductId = null);
    }
    
    public class IkasApiService : IIkasApiService
    {
        private readonly HttpClient _httpClient;
        
        public IkasApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        
        public async Task<IkasApiResponse> CreateOrUpdateProductAsync(Product product, SiteMapping mapping, string ikasProductId = null)
        {
            try
            {
                const string IKAS_API_BASE = "https://api.myikas.com";
                var apiUrl = $"{IKAS_API_BASE}/api/v1/products";
                if (!string.IsNullOrEmpty(ikasProductId))
                {
                    apiUrl += $"/{ikasProductId}";
                }
                
                // Prepare İkas product data
                var ikasProduct = new
                {
                    name = product.Name,
                    sku = product.Sku,
                    barcode = product.Barcode,
                    description = product.Description,
                    category = product.CategoryPath,
                    brand = product.Brand,
                    price = product.SalePrice,
                    comparePrice = product.DiscountPrice,
                    stock = product.StockQuantity,
                    weight = product.Weight,
                    isActive = product.IsActive
                };
                
                var jsonContent = JsonConvert.SerializeObject(ikasProduct);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Add authentication headers
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", mapping.IkasApiKey);
                _httpClient.DefaultRequestHeaders.Add("X-Api-Secret", mapping.IkasApiSecret);
                
                HttpResponseMessage response;
                if (string.IsNullOrEmpty(ikasProductId))
                {
                    // Create new product
                    response = await _httpClient.PostAsync(apiUrl, content);
                }
                else
                {
                    // Update existing product
                    response = await _httpClient.PutAsync(apiUrl, content);
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<IkasProductResponse>(responseContent);
                    return new IkasApiResponse
                    {
                        IsSuccess = true,
                        ProductId = result?.Id,
                        Message = "Success"
                    };
                }
                else
                {
                    return new IkasApiResponse
                    {
                        IsSuccess = false,
                        Message = $"İkas API Error: {response.StatusCode} - {responseContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new IkasApiResponse
                {
                    IsSuccess = false,
                    Message = $"Exception: {ex.Message}"
                };
            }
        }
    }
    
    public class IkasApiResponse
    {
        public bool IsSuccess { get; set; }
        public string ProductId { get; set; }
        public string Message { get; set; }
    }
    
    public class IkasProductResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
    }
}
```

**MultiSiteIkas.Core/Services/TransferService.cs**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MultiSiteIkas.Core.Models;
using MultiSiteIkas.Data.Repositories;
using Newtonsoft.Json;

namespace MultiSiteIkas.Core.Services
{
    public interface ITransferService
    {
        Task ProcessXmlTransferAsync(long xmlSourceId);
    }
    
    public class TransferService : ITransferService
    {
        private readonly IXmlParsingService _xmlParsingService;
        private readonly ICategoryFilterService _categoryFilterService;
        private readonly IPricingService _pricingService;
        private readonly IIkasApiService _ikasApiService;
        private readonly ISiteMappingRepository _siteMappingRepo;
        private readonly ITransferLogRepository _transferLogRepo;
        
        public TransferService(
            IXmlParsingService xmlParsingService,
            ICategoryFilterService categoryFilterService,
            IPricingService pricingService,
            IIkasApiService ikasApiService,
            ISiteMappingRepository siteMappingRepo,
            ITransferLogRepository transferLogRepo)
        {
            _xmlParsingService = xmlParsingService;
            _categoryFilterService = categoryFilterService;
            _pricingService = pricingService;
            _ikasApiService = ikasApiService;
            _siteMappingRepo = siteMappingRepo;
            _transferLogRepo = transferLogRepo;
        }
        
        public async Task ProcessXmlTransferAsync(long xmlSourceId)
        {
            // Get active site mappings
            var siteMappings = await _siteMappingRepo.GetActiveByXmlSourceIdAsync(xmlSourceId);
            
            if (!siteMappings.Any())
            {
                Console.WriteLine($"No active site mappings found for XML source {xmlSourceId}");
                return;
            }
            
            // Parse XML (assuming XML URL is stored in XmlSource)
            // Note: You'll need to fetch XmlSource details first
            var xmlUrl = "https://hobizubi.com/export/products.xml"; // TODO: Get from XmlSource
            var allProducts = await _xmlParsingService.ParseXmlAsync(xmlUrl, xmlSourceId);
            
            Console.WriteLine($"Parsed {allProducts.Count} products from XML");
            
            // Process each site mapping
            foreach (var mapping in siteMappings)
            {
                await ProcessSingleSiteTransferAsync(allProducts, mapping);
            }
        }
        
        private async Task ProcessSingleSiteTransferAsync(List<Product> allProducts, SiteMapping mapping)
        {
            var transferErrors = new List<TransferError>();
            int successCount = 0, errorCount = 0, skippedCount = 0;
            
            // Start transfer log
            var log = new TransferLog
            {
                XmlSourceId = mapping.XmlSourceId,
                SiteMappingId = mapping.Id,
                TargetCompanyId = mapping.TargetCompanyId,
                TotalProductsInXml = allProducts.Count,
                StartDate = DateTime.Now,
                Status = TransferStatus.InProgress
            };
            
            var logId = await _transferLogRepo.StartLogAsync(log);
            
            try
            {
                Console.WriteLine($"\n--- Processing Site: {mapping.TargetCompanyId} ---");
                
                // 1. Filter products by category
                var filteredProducts = allProducts
                    .Where(p => _categoryFilterService.IsProductAllowedForSite(p.CategoryPath, mapping))
                    .ToList();
                
                Console.WriteLine($"Filtered products: {filteredProducts.Count}/{allProducts.Count}");
                
                if (!filteredProducts.Any())
                {
                    log.FilteredProductsCount = 0;
                    log.Status = TransferStatus.Success;
                    await _transferLogRepo.CompleteLogAsync(logId, log);
                    return;
                }
                
                // 2. Process each product
                foreach (var product in filteredProducts)
                {
                    try
                    {
                        // Apply site-specific pricing
                        product.SalePrice = _pricingService.CalculateSitePrice(product.SalePrice, mapping);
                        if (product.DiscountPrice.HasValue)
                        {
                            product.DiscountPrice = _pricingService.CalculateSitePrice(product.DiscountPrice.Value, mapping);
                        }
                        
                        // Map category
                        product.CategoryPath = _categoryFilterService.MapCategory(product.CategoryPath, mapping);
                        
                        // Transfer to İkas
                        var result = await _ikasApiService.CreateOrUpdateProductAsync(product, mapping);
                        
                        if (result.IsSuccess)
                        {
                            successCount++;
                            Console.WriteLine($"✓ Success: {product.Sku} - {product.Name}");
                        }
                        else
                        {
                            errorCount++;
                            transferErrors.Add(new TransferError
                            {
                                Sku = product.Sku,
                                ProductName = product.Name,
                                ErrorMessage = result.Message,
                                ErrorDate = DateTime.Now
                            });
                            Console.WriteLine($"✗ Error: {product.Sku} - {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        transferErrors.Add(new TransferError
                        {
                            Sku = product.Sku,
                            ProductName = product.Name,
                            ErrorMessage = ex.Message,
                            ErrorDate = DateTime.Now
                        });
                        Console.WriteLine($"✗ Exception: {product.Sku} - {ex.Message}");
                    }
                }
                
                // 3. Complete transfer log
                log.FilteredProductsCount = filteredProducts.Count;
                log.SuccessCount = successCount;
                log.ErrorCount = errorCount;
                log.SkippedCount = skippedCount;
                log.Status = errorCount > 0 ? TransferStatus.Failed : TransferStatus.Success;
                log.ErrorDetails = transferErrors.Any() ? JsonConvert.SerializeObject(transferErrors) : null;
                
                await _transferLogRepo.CompleteLogAsync(logId, log);
                
                Console.WriteLine($"Transfer completed - Success: {successCount}, Errors: {errorCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error in site transfer: {ex.Message}");
                
                log.ErrorCount = 1;
                log.Status = TransferStatus.Failed;
                log.ErrorDetails = JsonConvert.SerializeObject(new[]
                {
                    new TransferError { ErrorMessage = ex.Message, ErrorDate = DateTime.Now }
                });
                
                await _transferLogRepo.CompleteLogAsync(logId, log);
            }
        }
    }
}
```

### Adım 6: API Layer - Controllers

**MultiSiteIkas.API/Controllers/SiteMappingController.cs**
```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.Core.Models;
using MultiSiteIkas.Data.Repositories;

namespace MultiSiteIkas.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SiteMappingController : ControllerBase
    {
        private readonly ISiteMappingRepository _repository;
        
        public SiteMappingController(ISiteMappingRepository repository)
        {
            _repository = repository;
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<SiteMapping>> GetById(long id)
        {
            var mapping = await _repository.GetByIdAsync(id);
            if (mapping == null)
                return NotFound();
            
            return Ok(mapping);
        }
        
        [HttpGet("xml-source/{xmlSourceId}")]
        public async Task<ActionResult<IEnumerable<SiteMapping>>> GetByXmlSourceId(long xmlSourceId)
        {
            var mappings = await _repository.GetByXmlSourceIdAsync(xmlSourceId);
            return Ok(mappings);
        }
        
        [HttpPost]
        public async Task<ActionResult<long>> Create([FromBody] SiteMapping mapping)
        {
            var id = await _repository.AddAsync(mapping);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }
        
        [HttpPut("{id}")]
        public async Task<ActionResult> Update(long id, [FromBody] SiteMapping mapping)
        {
            mapping.Id = id;
            await _repository.UpdateAsync(mapping);
            return NoContent();
        }
        
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            await _repository.DeleteAsync(id);
            return NoContent();
        }
    }
}
```

**MultiSiteIkas.API/Controllers/TransferController.cs**
```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.Core.Services;
using MultiSiteIkas.Data.Repositories;

namespace MultiSiteIkas.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransferController : ControllerBase
    {
        private readonly ITransferService _transferService;
        private readonly ITransferLogRepository _transferLogRepo;
        
        public TransferController(ITransferService transferService, ITransferLogRepository transferLogRepo)
        {
            _transferService = transferService;
            _transferLogRepo = transferLogRepo;
        }
        
        [HttpPost("start/{xmlSourceId}")]
        public async Task<ActionResult> StartTransfer(long xmlSourceId)
        {
            await _transferService.ProcessXmlTransferAsync(xmlSourceId);
            return Ok(new { message = "Transfer started successfully" });
        }
        
        [HttpGet("logs/{xmlSourceId}")]
        public async Task<ActionResult> GetLogs(long xmlSourceId, [FromQuery] int count = 50)
        {
            var logs = await _transferLogRepo.GetRecentLogsAsync(xmlSourceId, count);
            return Ok(logs);
        }
    }
}
```

**MultiSiteIkas.API/Program.cs**
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MultiSiteIkas.Core.Services;
using MultiSiteIkas.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register HttpClient
builder.Services.AddHttpClient<IXmlParsingService, XmlParsingService>();
builder.Services.AddHttpClient<IIkasApiService, IkasApiService>();

// Register repositories
builder.Services.AddScoped<ISiteMappingRepository>(sp => new SiteMappingRepository(connectionString));
builder.Services.AddScoped<ITransferLogRepository>(sp => new TransferLogRepository(connectionString));

// Register services
builder.Services.AddScoped<ICategoryFilterService, CategoryFilterService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<ITransferService, TransferService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
```

**MultiSiteIkas.API/appsettings.json**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MultiSiteIkasDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Adım 7: Background Jobs

**MultiSiteIkas.Jobs/Jobs/DailyXmlSyncJob.cs**
```csharp
using System;
using System.Threading.Tasks;
using Hangfire;
using MultiSiteIkas.Core.Services;

namespace MultiSiteIkas.Jobs.Jobs
{
    public class DailyXmlSyncJob
    {
        private readonly ITransferService _transferService;
        
        public DailyXmlSyncJob(ITransferService transferService)
        {
            _transferService = transferService;
        }
        
        public async Task Execute()
        {
            Console.WriteLine($"[{DateTime.Now}] Daily XML Sync Job started");
            
            try
            {
                // TODO: Get all active XML sources from database
                var xmlSourceIds = new long[] { 1 }; // Example
                
                foreach (var xmlSourceId in xmlSourceIds)
                {
                    await _transferService.ProcessXmlTransferAsync(xmlSourceId);
                }
                
                Console.WriteLine($"[{DateTime.Now}] Daily XML Sync Job completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] Daily XML Sync Job failed: {ex.Message}");
                throw;
            }
        }
    }
}
```

**MultiSiteIkas.Jobs/Program.cs**
```csharp
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MultiSiteIkas.Core.Services;
using MultiSiteIkas.Data.Repositories;
using MultiSiteIkas.Jobs.Jobs;
using System;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
    
    // Hangfire
    services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));
    
    services.AddHangfireServer();
    
    // Register dependencies
    services.AddHttpClient<IXmlParsingService, XmlParsingService>();
    services.AddHttpClient<IIkasApiService, IkasApiService>();
    services.AddScoped<ISiteMappingRepository>(sp => new SiteMappingRepository(connectionString));
    services.AddScoped<ITransferLogRepository>(sp => new TransferLogRepository(connectionString));
    services.AddScoped<ICategoryFilterService, CategoryFilterService>();
    services.AddScoped<IPricingService, PricingService>();
    services.AddScoped<ITransferService, TransferService>();
    services.AddScoped<DailyXmlSyncJob>();
});

var app = builder.Build();

// Schedule recurring jobs
RecurringJob.AddOrUpdate<DailyXmlSyncJob>(
    "daily-xml-sync",
    job => job.Execute(),
    "0 2 * * *",  // Every day at 02:00 AM
    TimeZoneInfo.Local
);

Console.WriteLine("Hangfire jobs configured. Press Ctrl+C to exit.");

await app.RunAsync();
```

### Adım 8: Projeyi Çalıştırma

```bash
# 1. Veritabanını oluştur
# SQL Server Management Studio'da yukarıdaki SQL scriptleri çalıştır

# 2. Connection string'i güncelle
# MultiSiteIkas.API/appsettings.json ve MultiSiteIkas.Jobs/appsettings.json

# 3. API'yi çalıştır
cd MultiSiteIkas.API
dotnet run

# 4. Jobs'u çalıştır (başka bir terminal)
cd MultiSiteIkas.Jobs
dotnet run

# 5. Swagger UI'da test et
# https://localhost:5001/swagger

# 6. Manuel transfer tetikle
curl -X POST https://localhost:5001/api/transfer/start/1
```

---

## 🟢 Node.js Implementation

### Proje Yapısı

```
multisite-ikas-nodejs/
├── src/
│   ├── api/
│   │   ├── controllers/
│   │   │   ├── siteMappingController.js
│   │   │   ├── transferController.js
│   │   │   └── dashboardController.js
│   │   ├── routes/
│   │   │   └── index.js
│   │   └── middlewares/
│   │       ├── errorHandler.js
│   │       └── validation.js
│   │
│   ├── services/
│   │   ├── xmlParsingService.js
│   │   ├── categoryFilterService.js
│   │   ├── pricingService.js
│   │   ├── ikasApiService.js
│   │   └── transferService.js
│   │
│   ├── repositories/
│   │   ├── siteMappingRepository.js
│   │   ├── productRepository.js
│   │   └── transferLogRepository.js
│   │
│   ├── models/
│   │   ├── Product.js
│   │   ├── SiteMapping.js
│   │   └── TransferLog.js
│   │
│   ├── jobs/
│   │   └── dailyXmlSyncJob.js
│   │
│   ├── config/
│   │   ├── database.js
│   │   └── app.js
│   │
│   └── utils/
│       ├── logger.js
│       └── helpers.js
│
├── tests/
│   ├── unit/
│   └── integration/
│
├── package.json
├── .env
├── .env.example
└── README.md
```

### Adım 1: Node.js Projesi Oluştur

```bash
# 1. Proje klasörü oluştur
mkdir multisite-ikas-nodejs
cd multisite-ikas-nodejs

# 2. package.json oluştur
npm init -y

# 3. Gerekli paketleri yükle
npm install express
npm install mssql              # SQL Server client
npm install axios              # HTTP client
npm install xml2js             # XML parser
npm install node-cron          # Job scheduler
npm install dotenv             # Environment variables
npm install winston            # Logger
npm install joi                # Validation
npm install cors               # CORS support

# 4. Dev dependencies
npm install --save-dev nodemon jest supertest

# 5. TypeScript (opsiyonel)
npm install --save-dev typescript @types/node @types/express
```

### Adım 2: Konfigürasyon

**.env**
```env
# Server
PORT=3000
NODE_ENV=development

# Database
DB_SERVER=localhost
DB_DATABASE=MultiSiteIkasDB
DB_USER=sa
DB_PASSWORD=YourPassword
DB_ENCRYPT=false
DB_TRUST_CERT=true

# Cron Jobs
SYNC_CRON_SCHEDULE=0 2 * * *

# Logging
LOG_LEVEL=info
```

**src/config/database.js**
```javascript
const sql = require('mssql');
require('dotenv').config();

const config = {
    server: process.env.DB_SERVER,
    database: process.env.DB_DATABASE,
    user: process.env.DB_USER,
    password: process.env.DB_PASSWORD,
    options: {
        encrypt: process.env.DB_ENCRYPT === 'true',
        trustServerCertificate: process.env.DB_TRUST_CERT === 'true',
    },
    pool: {
        max: 10,
        min: 0,
        idleTimeoutMillis: 30000
    }
};

let pool;

async function getPool() {
    if (!pool) {
        pool = await sql.connect(config);
    }
    return pool;
}

module.exports = { getPool, sql };
```

**src/config/app.js**
```javascript
module.exports = {
    port: process.env.PORT || 3000,
    env: process.env.NODE_ENV || 'development',
    syncCronSchedule: process.env.SYNC_CRON_SCHEDULE || '0 2 * * *',
};
```

### Adım 3: Models

**src/models/Product.js**
```javascript
class Product {
    constructor(data) {
        this.id = data.id;
        this.companyId = data.company_id || data.companyId;
        this.xmlSourceId = data.xml_source_id || data.xmlSourceId;
        this.sku = data.sku;
        this.barcode = data.barcode;
        this.name = data.name;
        this.description = data.description;
        this.categoryPath = data.category_path || data.categoryPath;
        this.brand = data.brand;
        this.originalPrice = parseFloat(data.original_price || data.originalPrice || 0);
        this.salePrice = parseFloat(data.sale_price || data.salePrice || 0);
        this.discountPrice = data.discount_price || data.discountPrice ? parseFloat(data.discount_price || data.discountPrice) : null;
        this.currency = data.currency || 'TRY';
        this.stockQuantity = parseInt(data.stock_quantity || data.stockQuantity || 0);
        this.weight = data.weight ? parseFloat(data.weight) : null;
        this.isActive = data.is_active !== undefined ? data.is_active : true;
        this.createdDate = data.created_date || data.createdDate || new Date();
        this.updatedDate = data.updated_date || data.updatedDate || null;
    }
}

module.exports = Product;
```

**src/models/SiteMapping.js**
```javascript
class SiteMapping {
    constructor(data) {
        this.id = data.id;
        this.xmlSourceId = data.xml_source_id || data.xmlSourceId;
        this.targetCompanyId = data.target_company_id || data.targetCompanyId;
        this.ikasApiKey = data.ikas_api_key || data.ikasApiKey;
        this.ikasApiSecret = data.ikas_api_secret || data.ikasApiSecret;
        this.priceMarginPercentage = parseFloat(data.price_margin_percentage || data.priceMarginPercentage || 0);
        this.additionalPrice = parseFloat(data.additional_price || data.additionalPrice || 0);
        this.categoryFilters = data.category_filters || data.categoryFilters;
        this.categoryMappings = data.category_mappings || data.categoryMappings;
        this.isActive = data.is_active !== undefined ? data.is_active : true;
        this.createdDate = data.created_date || data.createdDate || new Date();
        this.updatedDate = data.updated_date || data.updatedDate || null;
        
        // Parse JSON fields
        this.categoryFilterList = this.categoryFilters ? JSON.parse(this.categoryFilters) : [];
        this.categoryMappingDict = this.categoryMappings ? JSON.parse(this.categoryMappings) : {};
    }
}

module.exports = SiteMapping;
```

**src/models/TransferLog.js**
```javascript
class TransferLog {
    constructor(data = {}) {
        this.id = data.id;
        this.xmlSourceId = data.xml_source_id || data.xmlSourceId;
        this.siteMappingId = data.site_mapping_id || data.siteMappingId;
        this.targetCompanyId = data.target_company_id || data.targetCompanyId;
        this.totalProductsInXml = parseInt(data.total_products_in_xml || data.totalProductsInXml || 0);
        this.filteredProductsCount = parseInt(data.filtered_products_count || data.filteredProductsCount || 0);
        this.successCount = parseInt(data.success_count || data.successCount || 0);
        this.errorCount = parseInt(data.error_count || data.errorCount || 0);
        this.skippedCount = parseInt(data.skipped_count || data.skippedCount || 0);
        this.startDate = data.start_date || data.startDate || new Date();
        this.endDate = data.end_date || data.endDate || null;
        this.durationSeconds = data.duration_seconds || data.durationSeconds || null;
        this.status = data.status || 1; // 1=InProgress, 2=Success, 3=Failed
        this.errorDetails = data.error_details || data.errorDetails || null;
        this.errors = this.errorDetails ? JSON.parse(this.errorDetails) : [];
    }
}

module.exports = TransferLog;
```

### Adım 4: Repositories

**src/repositories/siteMappingRepository.js**
```javascript
const { getPool, sql } = require('../config/database');
const SiteMapping = require('../models/SiteMapping');

class SiteMappingRepository {
    async getById(id) {
        const pool = await getPool();
        const result = await pool.request()
            .input('id', sql.BigInt, id)
            .query('SELECT * FROM site_mappings WHERE id = @id');
        
        return result.recordset[0] ? new SiteMapping(result.recordset[0]) : null;
    }
    
    async getByXmlSourceId(xmlSourceId) {
        const pool = await getPool();
        const result = await pool.request()
            .input('xmlSourceId', sql.BigInt, xmlSourceId)
            .query('SELECT * FROM site_mappings WHERE xml_source_id = @xmlSourceId');
        
        return result.recordset.map(row => new SiteMapping(row));
    }
    
    async getActiveByXmlSourceId(xmlSourceId) {
        const pool = await getPool();
        const result = await pool.request()
            .input('xmlSourceId', sql.BigInt, xmlSourceId)
            .query('SELECT * FROM site_mappings WHERE xml_source_id = @xmlSourceId AND is_active = 1');
        
        return result.recordset.map(row => new SiteMapping(row));
    }
    
    async add(mapping) {
        const pool = await getPool();
        const result = await pool.request()
            .input('xmlSourceId', sql.BigInt, mapping.xmlSourceId)
            .input('targetCompanyId', sql.BigInt, mapping.targetCompanyId)
            .input('ikasApiKey', sql.NVarChar(500), mapping.ikasApiKey)
            .input('ikasApiSecret', sql.NVarChar(500), mapping.ikasApiSecret)
            .input('ikasStoreUrl', sql.NVarChar(500), mapping.ikasStoreUrl)
            .input('priceMarginPercentage', sql.Decimal(18, 2), mapping.priceMarginPercentage)
            .input('additionalPrice', sql.Decimal(18, 2), mapping.additionalPrice)
            .input('categoryFilters', sql.NVarChar(sql.MAX), mapping.categoryFilters)
            .input('categoryMappings', sql.NVarChar(sql.MAX), mapping.categoryMappings)
            .input('isActive', sql.Bit, mapping.isActive)
            .query(`
                INSERT INTO site_mappings 
                (xml_source_id, target_company_id, ikas_api_key, ikas_api_secret,
                 price_margin_percentage, additional_price, category_filters, category_mappings, is_active, created_date)
                VALUES 
                (@xmlSourceId, @targetCompanyId, @ikasApiKey, @ikasApiSecret,
                 @priceMarginPercentage, @additionalPrice, @categoryFilters, @categoryMappings, @isActive, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() as bigint) AS id
            `);
        
        return result.recordset[0].id;
    }
    
    async update(mapping) {
        const pool = await getPool();
        await pool.request()
            .input('id', sql.BigInt, mapping.id)
            .input('ikasApiKey', sql.NVarChar(500), mapping.ikasApiKey)
            .input('ikasApiSecret', sql.NVarChar(500), mapping.ikasApiSecret)
            .input('ikasStoreUrl', sql.NVarChar(500), mapping.ikasStoreUrl)
            .input('priceMarginPercentage', sql.Decimal(18, 2), mapping.priceMarginPercentage)
            .input('additionalPrice', sql.Decimal(18, 2), mapping.additionalPrice)
            .input('categoryFilters', sql.NVarChar(sql.MAX), mapping.categoryFilters)
            .input('categoryMappings', sql.NVarChar(sql.MAX), mapping.categoryMappings)
            .input('isActive', sql.Bit, mapping.isActive)
            .query(`
                UPDATE site_mappings 
                SET ikas_api_key = @ikasApiKey,
                    ikas_api_secret = @ikasApiSecret,
                    price_margin_percentage = @priceMarginPercentage,
                    additional_price = @additionalPrice,
                    category_filters = @categoryFilters,
                    category_mappings = @categoryMappings,
                    is_active = @isActive,
                    updated_date = GETDATE()
                WHERE id = @id
            `);
    }
    
    async delete(id) {
        const pool = await getPool();
        await pool.request()
            .input('id', sql.BigInt, id)
            .query('DELETE FROM site_mappings WHERE id = @id');
    }
}

module.exports = new SiteMappingRepository();
```

**src/repositories/transferLogRepository.js**
```javascript
const { getPool, sql } = require('../config/database');
const TransferLog = require('../models/TransferLog');

class TransferLogRepository {
    async startLog(log) {
        const pool = await getPool();
        const result = await pool.request()
            .input('xmlSourceId', sql.BigInt, log.xmlSourceId)
            .input('siteMappingId', sql.BigInt, log.siteMappingId)
            .input('targetCompanyId', sql.BigInt, log.targetCompanyId)
            .input('totalProductsInXml', sql.Int, log.totalProductsInXml)
            .query(`
                INSERT INTO transfer_logs 
                (xml_source_id, site_mapping_id, target_company_id, total_products_in_xml, start_date, status)
                VALUES 
                (@xmlSourceId, @siteMappingId, @targetCompanyId, @totalProductsInXml, GETDATE(), 1);
                SELECT CAST(SCOPE_IDENTITY() as bigint) AS id
            `);
        
        return result.recordset[0].id;
    }
    
    async completeLog(logId, log) {
        const pool = await getPool();
        await pool.request()
            .input('id', sql.BigInt, logId)
            .input('filteredProductsCount', sql.Int, log.filteredProductsCount)
            .input('successCount', sql.Int, log.successCount)
            .input('errorCount', sql.Int, log.errorCount)
            .input('skippedCount', sql.Int, log.skippedCount)
            .input('errorDetails', sql.NVarChar(sql.MAX), log.errorDetails)
            .input('status', sql.Int, log.status)
            .query(`
                UPDATE transfer_logs 
                SET end_date = GETDATE(),
                    duration_seconds = DATEDIFF(SECOND, start_date, GETDATE()),
                    filtered_products_count = @filteredProductsCount,
                    success_count = @successCount,
                    error_count = @errorCount,
                    skipped_count = @skippedCount,
                    error_details = @errorDetails,
                    status = @status
                WHERE id = @id
            `);
    }
    
    async getRecentLogs(xmlSourceId, count = 50) {
        const pool = await getPool();
        const result = await pool.request()
            .input('xmlSourceId', sql.BigInt, xmlSourceId)
            .input('count', sql.Int, count)
            .query(`
                SELECT TOP (@count) * 
                FROM transfer_logs 
                WHERE xml_source_id = @xmlSourceId
                ORDER BY start_date DESC
            `);
        
        return result.recordset.map(row => new TransferLog(row));
    }
}

module.exports = new TransferLogRepository();
```

### Adım 5: Services

**src/services/categoryFilterService.js**
```javascript
class CategoryFilterService {
    isProductAllowedForSite(productCategory, mapping) {
        if (!mapping.categoryFilterList || mapping.categoryFilterList.length === 0) {
            return true; // No filter = allow all
        }
        
        for (const filter of mapping.categoryFilterList) {
            // Wildcard support: "Boncuk > *"
            if (filter.includes(' > *') || filter.includes('>*')) {
                const parentCategory = filter.replace(' > *', '').replace('>*', '').trim();
                if (productCategory.toLowerCase().startsWith(parentCategory.toLowerCase())) {
                    return true;
                }
            }
            // Exact or partial match
            else if (productCategory.toLowerCase().includes(filter.toLowerCase())) {
                return true;
            }
        }
        
        return false;
    }
    
    mapCategory(sourceCategory, mapping) {
        if (!mapping.categoryMappingDict || Object.keys(mapping.categoryMappingDict).length === 0) {
            return sourceCategory;
        }
        
        // Split by " > " and map each level
        const categories = sourceCategory.split(' > ').map(c => c.trim());
        const mappedCategories = categories.map(cat => {
            return mapping.categoryMappingDict[cat] || cat;
        });
        
        return mappedCategories.join(' > ');
    }
}

module.exports = new CategoryFilterService();
```

**src/services/pricingService.js**
```javascript
class PricingService {
    calculateSitePrice(originalPrice, mapping) {
        // Step 1: Apply percentage margin
        const priceWithMargin = originalPrice * (1 + (mapping.priceMarginPercentage / 100));
        
        // Step 2: Add fixed price
        const finalPrice = priceWithMargin + mapping.additionalPrice;
        
        // Step 3: Round to 2 decimals
        return Math.round(finalPrice * 100) / 100;
    }
}

module.exports = new PricingService();
```

**src/services/xmlParsingService.js**
```javascript
const axios = require('axios');
const xml2js = require('xml2js');
const Product = require('../models/Product');

class XmlParsingService {
    async parseXml(xmlUrl, xmlSourceId) {
        try {
            // Download XML
            const response = await axios.get(xmlUrl);
            const xmlContent = response.data;
            
            // Parse XML to JSON
            const parser = new xml2js.Parser();
            const result = await parser.parseStringPromise(xmlContent);
            
            // Extract products (adjust based on your XML structure)
            const productElements = result.products?.product || [];
            
            const products = productElements.map(element => {
                return new Product({
                    xmlSourceId: xmlSourceId,
                    sku: element.sku?.[0],
                    barcode: element.barcode?.[0],
                    name: element.name?.[0],
                    description: element.description?.[0],
                    categoryPath: element.category?.[0],
                    brand: element.brand?.[0],
                    originalPrice: parseFloat(element.price?.[0] || 0),
                    salePrice: parseFloat(element.sale_price?.[0] || element.price?.[0] || 0),
                    discountPrice: element.discount_price?.[0] ? parseFloat(element.discount_price[0]) : null,
                    stockQuantity: parseInt(element.stock?.[0] || 0),
                    weight: element.weight?.[0] ? parseFloat(element.weight[0]) : null,
                    isActive: true,
                    createdDate: new Date()
                });
            });
            
            return products;
        } catch (error) {
            throw new Error(`XML parsing error: ${error.message}`);
        }
    }
}

module.exports = new XmlParsingService();
```

**src/services/ikasApiService.js**
```javascript
const axios = require('axios');

class IkasApiService {
    async createOrUpdateProduct(product, mapping, ikasProductId = null) {
        try {
            const IKAS_API_BASE = 'https://api.myikas.com';
            const apiUrl = ikasProductId
                ? `${IKAS_API_BASE}/api/v1/products/${ikasProductId}`
                : `${IKAS_API_BASE}/api/v1/products`;
            
            // Prepare İkas product data
            const ikasProduct = {
                name: product.name,
                sku: product.sku,
                barcode: product.barcode,
                description: product.description,
                category: product.categoryPath,
                brand: product.brand,
                price: product.salePrice,
                comparePrice: product.discountPrice,
                stock: product.stockQuantity,
                weight: product.weight,
                isActive: product.isActive
            };
            
            const headers = {
                'Content-Type': 'application/json',
                'X-Api-Key': mapping.ikasApiKey,
                'X-Api-Secret': mapping.ikasApiSecret
            };
            
            let response;
            if (ikasProductId) {
                // Update existing product
                response = await axios.put(apiUrl, ikasProduct, { headers });
            } else {
                // Create new product
                response = await axios.post(apiUrl, ikasProduct, { headers });
            }
            
            return {
                isSuccess: true,
                productId: response.data?.id,
                message: 'Success'
            };
        } catch (error) {
            return {
                isSuccess: false,
                message: error.response?.data?.message || error.message
            };
        }
    }
}

module.exports = new IkasApiService();
```

**src/services/transferService.js**
```javascript
const xmlParsingService = require('./xmlParsingService');
const categoryFilterService = require('./categoryFilterService');
const pricingService = require('./pricingService');
const ikasApiService = require('./ikasApiService');
const siteMappingRepository = require('../repositories/siteMappingRepository');
const transferLogRepository = require('../repositories/transferLogRepository');
const TransferLog = require('../models/TransferLog');

class TransferService {
    async processXmlTransfer(xmlSourceId, xmlUrl) {
        console.log(`Processing XML transfer for source: ${xmlSourceId}`);
        
        // Get active site mappings
        const siteMappings = await siteMappingRepository.getActiveByXmlSourceId(xmlSourceId);
        
        if (siteMappings.length === 0) {
            console.log(`No active site mappings found for XML source ${xmlSourceId}`);
            return;
        }
        
        // Parse XML
        const allProducts = await xmlParsingService.parseXml(xmlUrl, xmlSourceId);
        console.log(`Parsed ${allProducts.length} products from XML`);
        
        // Process each site mapping
        for (const mapping of siteMappings) {
            await this.processSingleSiteTransfer(allProducts, mapping);
        }
    }
    
    async processSingleSiteTransfer(allProducts, mapping) {
        const transferErrors = [];
        let successCount = 0;
        let errorCount = 0;
        let skippedCount = 0;
        
        // Start transfer log
        const log = new TransferLog({
            xmlSourceId: mapping.xmlSourceId,
            siteMappingId: mapping.id,
            targetCompanyId: mapping.targetCompanyId,
            totalProductsInXml: allProducts.length,
            status: 1 // InProgress
        });
        
        const logId = await transferLogRepository.startLog(log);
        
        try {
            console.log(`\n--- Processing Site: ${mapping.targetCompanyId} ---`);
            
            // 1. Filter products by category
            const filteredProducts = allProducts.filter(p =>
                categoryFilterService.isProductAllowedForSite(p.categoryPath, mapping)
            );
            
            console.log(`Filtered products: ${filteredProducts.length}/${allProducts.length}`);
            
            if (filteredProducts.length === 0) {
                log.filteredProductsCount = 0;
                log.status = 2; // Success
                await transferLogRepository.completeLog(logId, log);
                return;
            }
            
            // 2. Process each product
            for (const product of filteredProducts) {
                try {
                    // Apply site-specific pricing
                    product.salePrice = pricingService.calculateSitePrice(product.salePrice, mapping);
                    if (product.discountPrice) {
                        product.discountPrice = pricingService.calculateSitePrice(product.discountPrice, mapping);
                    }
                    
                    // Map category
                    product.categoryPath = categoryFilterService.mapCategory(product.categoryPath, mapping);
                    
                    // Transfer to İkas
                    const result = await ikasApiService.createOrUpdateProduct(product, mapping);
                    
                    if (result.isSuccess) {
                        successCount++;
                        console.log(`✓ Success: ${product.sku} - ${product.name}`);
                    } else {
                        errorCount++;
                        transferErrors.push({
                            sku: product.sku,
                            productName: product.name,
                            errorMessage: result.message,
                            errorDate: new Date()
                        });
                        console.log(`✗ Error: ${product.sku} - ${result.message}`);
                    }
                } catch (ex) {
                    errorCount++;
                    transferErrors.push({
                        sku: product.sku,
                        productName: product.name,
                        errorMessage: ex.message,
                        errorDate: new Date()
                    });
                    console.log(`✗ Exception: ${product.sku} - ${ex.message}`);
                }
            }
            
            // 3. Complete transfer log
            log.filteredProductsCount = filteredProducts.length;
            log.successCount = successCount;
            log.errorCount = errorCount;
            log.skippedCount = skippedCount;
            log.status = errorCount > 0 ? 3 : 2; // Failed : Success
            log.errorDetails = transferErrors.length > 0 ? JSON.stringify(transferErrors) : null;
            
            await transferLogRepository.completeLog(logId, log);
            
            console.log(`Transfer completed - Success: ${successCount}, Errors: ${errorCount}`);
        } catch (ex) {
            console.log(`Fatal error in site transfer: ${ex.message}`);
            
            log.errorCount = 1;
            log.status = 3; // Failed
            log.errorDetails = JSON.stringify([{
                errorMessage: ex.message,
                errorDate: new Date()
            }]);
            
            await transferLogRepository.completeLog(logId, log);
        }
    }
}

module.exports = new TransferService();
```

### Adım 6: API Controllers

**src/api/controllers/siteMappingController.js**
```javascript
const siteMappingRepository = require('../../repositories/siteMappingRepository');
const SiteMapping = require('../../models/SiteMapping');

class SiteMappingController {
    async getById(req, res, next) {
        try {
            const { id } = req.params;
            const mapping = await siteMappingRepository.getById(id);
            
            if (!mapping) {
                return res.status(404).json({ message: 'Site mapping not found' });
            }
            
            res.json(mapping);
        } catch (error) {
            next(error);
        }
    }
    
    async getByXmlSourceId(req, res, next) {
        try {
            const { xmlSourceId } = req.params;
            const mappings = await siteMappingRepository.getByXmlSourceId(xmlSourceId);
            res.json(mappings);
        } catch (error) {
            next(error);
        }
    }
    
    async create(req, res, next) {
        try {
            const mapping = new SiteMapping(req.body);
            const id = await siteMappingRepository.add(mapping);
            res.status(201).json({ id, message: 'Site mapping created successfully' });
        } catch (error) {
            next(error);
        }
    }
    
    async update(req, res, next) {
        try {
            const { id } = req.params;
            const mapping = new SiteMapping({ ...req.body, id });
            await siteMappingRepository.update(mapping);
            res.json({ message: 'Site mapping updated successfully' });
        } catch (error) {
            next(error);
        }
    }
    
    async delete(req, res, next) {
        try {
            const { id } = req.params;
            await siteMappingRepository.delete(id);
            res.json({ message: 'Site mapping deleted successfully' });
        } catch (error) {
            next(error);
        }
    }
}

module.exports = new SiteMappingController();
```

**src/api/controllers/transferController.js**
```javascript
const transferService = require('../../services/transferService');
const transferLogRepository = require('../../repositories/transferLogRepository');

class TransferController {
    async startTransfer(req, res, next) {
        try {
            const { xmlSourceId } = req.params;
            const { xmlUrl } = req.body;
            
            // Start transfer asynchronously
            transferService.processXmlTransfer(xmlSourceId, xmlUrl)
                .catch(err => console.error('Transfer error:', err));
            
            res.json({ message: 'Transfer started successfully' });
        } catch (error) {
            next(error);
        }
    }
    
    async getLogs(req, res, next) {
        try {
            const { xmlSourceId } = req.params;
            const { count = 50 } = req.query;
            
            const logs = await transferLogRepository.getRecentLogs(xmlSourceId, parseInt(count));
            res.json(logs);
        } catch (error) {
            next(error);
        }
    }
}

module.exports = new TransferController();
```

**src/api/routes/index.js**
```javascript
const express = require('express');
const siteMappingController = require('../controllers/siteMappingController');
const transferController = require('../controllers/transferController');

const router = express.Router();

// Site Mapping Routes
router.get('/site-mappings/:id', siteMappingController.getById);
router.get('/site-mappings/xml-source/:xmlSourceId', siteMappingController.getByXmlSourceId);
router.post('/site-mappings', siteMappingController.create);
router.put('/site-mappings/:id', siteMappingController.update);
router.delete('/site-mappings/:id', siteMappingController.delete);

// Transfer Routes
router.post('/transfer/start/:xmlSourceId', transferController.startTransfer);
router.get('/transfer/logs/:xmlSourceId', transferController.getLogs);

module.exports = router;
```

**src/api/middlewares/errorHandler.js**
```javascript
function errorHandler(err, req, res, next) {
    console.error('Error:', err);
    
    res.status(err.status || 500).json({
        message: err.message || 'Internal Server Error',
        error: process.env.NODE_ENV === 'development' ? err : {}
    });
}

module.exports = errorHandler;
```

### Adım 7: Main Application

**src/app.js**
```javascript
const express = require('express');
const cors = require('cors');
const routes = require('./api/routes');
const errorHandler = require('./api/middlewares/errorHandler');
const config = require('./config/app');

const app = express();

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// Routes
app.use('/api', routes);

// Health check
app.get('/health', (req, res) => {
    res.json({ status: 'OK', timestamp: new Date() });
});

// Error handler (must be last)
app.use(errorHandler);

// Start server
const PORT = config.port;
app.listen(PORT, () => {
    console.log(`Server running on port ${PORT}`);
    console.log(`Environment: ${config.env}`);
    console.log(`Health check: http://localhost:${PORT}/health`);
});

module.exports = app;
```

### Adım 8: Background Jobs

**src/jobs/dailyXmlSyncJob.js**
```javascript
const cron = require('node-cron');
const transferService = require('../services/transferService');
const config = require('../config/app');

class DailyXmlSyncJob {
    start() {
        console.log(`Scheduling daily XML sync job: ${config.syncCronSchedule}`);
        
        cron.schedule(config.syncCronSchedule, async () => {
            console.log(`[${new Date()}] Daily XML Sync Job started`);
            
            try {
                // TODO: Get all active XML sources from database
                const xmlSources = [
                    { id: 1, url: 'https://hobizubi.com/export/products.xml' }
                ];
                
                for (const source of xmlSources) {
                    await transferService.processXmlTransfer(source.id, source.url);
                }
                
                console.log(`[${new Date()}] Daily XML Sync Job completed successfully`);
            } catch (error) {
                console.error(`[${new Date()}] Daily XML Sync Job failed:`, error);
            }
        });
    }
}

module.exports = new DailyXmlSyncJob();
```

**src/index.js** (Entry Point)
```javascript
require('dotenv').config();
require('./app'); // Start Express server
const dailyXmlSyncJob = require('./jobs/dailyXmlSyncJob');

// Start background jobs
dailyXmlSyncJob.start();

console.log('Multi-Site İkas Distribution System started!');
```

**package.json**
```json
{
  "name": "multisite-ikas-nodejs",
  "version": "1.0.0",
  "description": "Multi-site Ikas product distribution system",
  "main": "src/index.js",
  "scripts": {
    "start": "node src/index.js",
    "dev": "nodemon src/index.js",
    "test": "jest"
  },
  "keywords": ["ikas", "xml", "product", "distribution"],
  "author": "",
  "license": "MIT",
  "dependencies": {
    "express": "^4.18.2",
    "mssql": "^10.0.1",
    "axios": "^1.6.2",
    "xml2js": "^0.6.2",
    "node-cron": "^3.0.3",
    "dotenv": "^16.3.1",
    "winston": "^3.11.0",
    "joi": "^17.11.0",
    "cors": "^2.8.5"
  },
  "devDependencies": {
    "nodemon": "^3.0.2",
    "jest": "^29.7.0",
    "supertest": "^6.3.3"
  }
}
```

### Adım 9: Projeyi Çalıştırma

```bash
# 1. Veritabanını oluştur
# SQL Server'da yukarıdaki SQL scriptleri çalıştır

# 2. .env dosyasını oluştur ve düzenle
cp .env.example .env
# DB bilgilerini gir

# 3. Dependencies yükle
npm install

# 4. Development modda çalıştır
npm run dev

# 5. API'yi test et
curl http://localhost:3000/health
curl http://localhost:3000/api/site-mappings/xml-source/1

# 6. Manuel transfer tetikle
curl -X POST http://localhost:3000/api/transfer/start/1 \
  -H "Content-Type: application/json" \
  -d '{"xmlUrl": "https://hobizubi.com/export/products.xml"}'
```

---

## 🎨 Frontend Dashboard

### React + Vite Kurulum

```bash
# 1. React projesi oluştur
npm create vite@latest multisite-ikas-dashboard -- --template react

cd multisite-ikas-dashboard

# 2. Dependencies
npm install
npm install axios react-router-dom
npm install @mui/material @mui/icons-material @emotion/react @emotion/styled
npm install recharts

# 3. Çalıştır
npm run dev
```

### Dashboard Sayfaları

**src/pages/Dashboard.jsx**
```jsx
import React, { useEffect, useState } from 'react';
import axios from 'axios';
import { 
    Card, CardContent, Typography, Grid, Box,
    Table, TableBody, TableCell, TableHead, TableRow 
} from '@mui/material';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend } from 'recharts';

function Dashboard() {
    const [stats, setStats] = useState(null);
    const [recentLogs, setRecentLogs] = useState([]);
    
    useEffect(() => {
        fetchDashboardData();
    }, []);
    
    const fetchDashboardData = async () => {
        try {
            const logsResponse = await axios.get('http://localhost:3000/api/transfer/logs/1?count=10');
            setRecentLogs(logsResponse.data);
            
            // Calculate stats
            const totalSuccess = logsResponse.data.reduce((sum, log) => sum + log.successCount, 0);
            const totalErrors = logsResponse.data.reduce((sum, log) => sum + log.errorCount, 0);
            const avgDuration = logsResponse.data.reduce((sum, log) => sum + (log.durationSeconds || 0), 0) / logsResponse.data.length;
            
            setStats({
                totalTransfers: logsResponse.data.length,
                totalSuccess,
                totalErrors,
                avgDuration: Math.round(avgDuration)
            });
        } catch (error) {
            console.error('Error fetching dashboard data:', error);
        }
    };
    
    return (
        <Box p={3}>
            <Typography variant="h4" gutterBottom>
                Multi-Site İkas Dashboard
            </Typography>
            
            {/* Stats Cards */}
            <Grid container spacing={3} mb={3}>
                <Grid item xs={12} sm={6} md={3}>
                    <Card>
                        <CardContent>
                            <Typography color="textSecondary" gutterBottom>
                                Total Transfers
                            </Typography>
                            <Typography variant="h4">
                                {stats?.totalTransfers || 0}
                            </Typography>
                        </CardContent>
                    </Card>
                </Grid>
                <Grid item xs={12} sm={6} md={3}>
                    <Card>
                        <CardContent>
                            <Typography color="textSecondary" gutterBottom>
                                Successful Products
                            </Typography>
                            <Typography variant="h4" color="success.main">
                                {stats?.totalSuccess || 0}
                            </Typography>
                        </CardContent>
                    </Card>
                </Grid>
                <Grid item xs={12} sm={6} md={3}>
                    <Card>
                        <CardContent>
                            <Typography color="textSecondary" gutterBottom>
                                Errors
                            </Typography>
                            <Typography variant="h4" color="error.main">
                                {stats?.totalErrors || 0}
                            </Typography>
                        </CardContent>
                    </Card>
                </Grid>
                <Grid item xs={12} sm={6} md={3}>
                    <Card>
                        <CardContent>
                            <Typography color="textSecondary" gutterBottom>
                                Avg Duration (sec)
                            </Typography>
                            <Typography variant="h4">
                                {stats?.avgDuration || 0}
                            </Typography>
                        </CardContent>
                    </Card>
                </Grid>
            </Grid>
            
            {/* Recent Transfers */}
            <Card>
                <CardContent>
                    <Typography variant="h6" gutterBottom>
                        Recent Transfers
                    </Typography>
                    <Table>
                        <TableHead>
                            <TableRow>
                                <TableCell>Date</TableCell>
                                <TableCell>Target Site</TableCell>
                                <TableCell align="right">Total</TableCell>
                                <TableCell align="right">Success</TableCell>
                                <TableCell align="right">Errors</TableCell>
                                <TableCell align="right">Duration</TableCell>
                                <TableCell>Status</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {recentLogs.map((log) => (
                                <TableRow key={log.id}>
                                    <TableCell>
                                        {new Date(log.startDate).toLocaleString()}
                                    </TableCell>
                                    <TableCell>{log.targetCompanyId}</TableCell>
                                    <TableCell align="right">{log.filteredProductsCount}</TableCell>
                                    <TableCell align="right" sx={{ color: 'success.main' }}>
                                        {log.successCount}
                                    </TableCell>
                                    <TableCell align="right" sx={{ color: 'error.main' }}>
                                        {log.errorCount}
                                    </TableCell>
                                    <TableCell align="right">{log.durationSeconds}s</TableCell>
                                    <TableCell>
                                        {log.status === 2 ? '✓ Success' : log.status === 3 ? '✗ Failed' : '⏳ In Progress'}
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>
        </Box>
    );
}

export default Dashboard;
```

---

## 🚀 Deployment

### Docker (Opsiyonel)

**Dockerfile (Node.js)**
```dockerfile
FROM node:18-alpine

WORKDIR /app

COPY package*.json ./
RUN npm ci --only=production

COPY src ./src

EXPOSE 3000

CMD ["node", "src/index.js"]
```

**docker-compose.yml**
```yaml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "3000:3000"
    environment:
      - NODE_ENV=production
      - DB_SERVER=sqlserver
      - DB_DATABASE=MultiSiteIkasDB
      - DB_USER=sa
      - DB_PASSWORD=YourPassword
    depends_on:
      - sqlserver
  
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourPassword
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql

volumes:
  sqldata:
```

---

## 📝 Özet: Hangi Yolu Seçmeliyim?

### C# Seçimi İçin:
✅ Mevcut ekip C# biliyor  
✅ .NET ekosistemi tercih ediliyor  
✅ Enterprise grade altyapı gerekli  
✅ Entity Framework / Dapper kullanmak istiyorsun  

### Node.js Seçimi İçin:
✅ Hızlı prototipleme gerekli  
✅ JavaScript/TypeScript biliniyor  
✅ Mikroservis mimarisi tercih ediliyor  
✅ Cloud-native deployment (Docker, K8s) planlı  

---

Her iki implementasyon da aynı özellikleri sağlar. Ekibinizin bildiği teknolojiye ve altyapı tercihlerinize göre seçim yapabilirsiniz! 🚀

---

## 💰 Maliyet Analizi ve Budget Planlama

### Senaryo: 10,000 Ürün + 4 Hedef Site

#### Sistem Gereksinimleri

**Ürün Profili:**
- Toplam ürün sayısı: 10,000 adet
- Hedef site sayısı: 4 (recinem, boncukpasaji, kalipatolyesi, mallofmolds)
- Ortalama ürün görseli: 3 adet/ürün (200 KB/görsel)
- Günlük sync: 1 kere
- Ürün verisi boyutu: ~500 KB/ürün (XML + metadata)

**API Çağrı Hacmi:**
- İlk transfer: 10,000 ürün × 4 site = **40,000 API çağrısı**
- Günlük güncelleme: ~10% değişiklik = 1,000 × 4 = **4,000 API çağrısı/gün**
- Aylık: 4,000 × 30 = **120,000 API çağrısı/ay**

**Veri Depolama:**
- Ürün metadatası: 10,000 × 500 KB = **5 GB**
- Görseller (CDN'de): 10,000 × 3 × 200 KB = **6 GB**
- Veritabanı (ürün + log + mapping): **~2 GB**
- Transfer log'ları (1 yıl): **~500 MB**

---

## 💵 Senaryo 1: Ekonomik Paket (DigitalOcean/Hetzner)

### Infrastructure

| Hizmet | Açıklama | Aylık Maliyet (TL) |
|--------|----------|-------------------|
| **Virtual Server** | 4 vCPU, 8 GB RAM, 160 GB SSD | ₺400 |
| **SQL Server** | Azure SQL Basic (2 GB) veya PostgreSQL self-hosted | ₺250 |
| **Object Storage** | 10 GB (görseller için) | ₺15 |
| **CDN** | Cloudflare Free (sınırsız bandwidth) | ₺0 |
| **Backup** | Weekly backup (50 GB) | ₺50 |
| **Domain + SSL** | Let's Encrypt SSL (free) + domain | ₺100 |
| **Monitoring** | UptimeRobot (free) + Basic logging | ₺0 |

**Toplam Aylık Maliyet:** **~₺815/ay**  
**Yıllık Maliyet:** **~₺9,780/yıl**

### Detaylar

**Server Seçenekleri:**
- **DigitalOcean Droplet:** $15/ay (~₺400) - 2 vCPU, 4 GB RAM
- **Hetzner Cloud:** €15/ay (~₺350) - 4 vCPU, 8 GB RAM ⭐ Önerilen
- **VultrHF:** $18/ay (~₺480) - 2 vCPU, 4 GB RAM

**Veritabanı:**
- **Option 1:** PostgreSQL (self-hosted, server içinde) - ₺0 ek
- **Option 2:** Azure SQL Basic - ₺250/ay
- **Option 3:** Supabase Free tier - ₺0 (50 GB limiti) ⭐ Önerilen başlangıç için

**Storage:**
- **Backblaze B2:** $0.005/GB/ay - 10 GB = $0.05/ay (~₺1.5)
- **Cloudflare R2:** İlk 10 GB free ⭐ Önerilen
- **AWS S3:** ~$0.023/GB/ay = ₺15/ay

### Avantajlar
✅ Düşük başlangıç maliyeti  
✅ Ölçeklenebilir (daha sonra upgrade)  
✅ Sabit aylık ücret  
✅ Self-managed (tam kontrol)

### Dezavantajlar
❌ Manuel yönetim gerekli  
❌ SLA garantisi yok  
❌ Managed services yok  
❌ Teknik bilgi gerekli

---

## 💵 Senaryo 2: Orta Seviye (Azure/AWS Small Business)

### Infrastructure

| Hizmet | Açıklama | Aylık Maliyet (TL) |
|--------|----------|-------------------|
| **App Service** | Azure App Service B2 (3.5 GB RAM) | ₺850 |
| **SQL Database** | Azure SQL S1 (20 DTU, 250 GB) | ₺900 |
| **Blob Storage** | 10 GB + 150 GB egress/ay | ₺150 |
| **Application Insights** | Monitoring + logging (5 GB/ay) | ₺300 |
| **Azure CDN** | 100 GB egress | ₺200 |
| **Backup** | Geo-redundant backup | ₺150 |
| **Load Balancer** | Basic load balancer | ₺250 |

**Toplam Aylık Maliyet:** **~₺2,800/ay**  
**Yıllık Maliyet:** **~₺33,600/yıl**

### Detaylar

**Compute:**
- **Azure App Service B2:** 2 vCPU, 3.5 GB RAM, Auto-scaling
- **AWS EC2 t3.medium:** 2 vCPU, 4 GB RAM, Reserved Instance (1 year)

**Database:**
- **Azure SQL S1:** 20 DTU, 250 GB storage, 99.99% SLA
- **AWS RDS SQL Server Express:** db.t3.small, 20 GB storage
- **Managed PostgreSQL:** Azure/AWS, ~₺600/ay (daha ucuz alternatif)

**Storage + CDN:**
- **Azure Blob Storage:** Standard tier, LRS
- **AWS S3 + CloudFront:** Similar pricing

### Avantajlar
✅ Managed services (az maintenance)  
✅ 99.99% SLA garantisi  
✅ Auto-scaling  
✅ Built-in monitoring  
✅ Automatic backup  
✅ Güvenlik güncellemeleri otomatik

### Dezavantajlar
❌ Daha yüksek maliyet  
❌ Vendor lock-in  
❌ Kompleks pricing modeli  
❌ Kullanmadığın özellikler için ödeme

---

## 💵 Senaryo 3: Enterprise (Azure/AWS Fully Managed)

### Infrastructure

| Hizmet | Açıklama | Aylık Maliyet (TL) |
|--------|----------|-------------------|
| **App Service Premium** | P1V3 (8 GB RAM, auto-scale 2-5 instances) | ₺3,500 |
| **SQL Database Business Critical** | BC Gen5 (4 vCore, 1 TB storage) | ₺8,500 |
| **Azure Storage Premium** | 50 GB + CDN Premium | ₺800 |
| **Application Insights Premium** | 20 GB ingestion/ay | ₺1,200 |
| **Azure Monitor** | Alerts + dashboards | ₺400 |
| **Azure DevOps** | CI/CD pipelines | ₺500 |
| **WAF + DDoS Protection** | Security | ₺2,000 |
| **Support Plan** | Professional Direct | ₺3,000 |

**Toplam Aylık Maliyet:** **~₺19,900/ay**  
**Yıllık Maliyet:** **~₺238,800/yıl**

### Avantajlar
✅ Enterprise-grade SLA (99.995%)  
✅ Full managed + support 24/7  
✅ Advanced security (WAF, DDoS, encryption)  
✅ Auto-scaling + load balancing  
✅ Global CDN  
✅ DevOps automation  
✅ Compliance certifications

### Dezavantajlar
❌ Çok yüksek maliyet  
❌ Over-engineering (10K ürün için gereksiz)  
❌ Kompleks setup

---

## 📊 Maliyet Karşılaştırma Tablosu

| Özellik | Ekonomik | Orta Seviye | Enterprise |
|---------|----------|-------------|------------|
| **Aylık Maliyet** | ₺815 | ₺2,800 | ₺19,900 |
| **Yıllık Maliyet** | ₺9,780 | ₺33,600 | ₺238,800 |
| **Setup Süresi** | 1-2 gün | 3-5 gün | 1-2 hafta |
| **Maintenance** | Yüksek (manuel) | Orta (semi-managed) | Düşük (full managed) |
| **Uptime SLA** | ~99.5% | 99.9% | 99.99% |
| **Scaling** | Manuel | Semi-auto | Full auto |
| **Support** | Community | Email/ticket | 24/7 phone |
| **İdeal Senaryo** | Startup, test | Growing business | Enterprise |

---

## 🎯 Önerilen Yaklaşım: Hibrit Model

### Faz 1: MVP (İlk 3-6 Ay) - **₺815/ay**

**Ekonomik paket ile başla:**
- Hetzner Cloud (₺350/ay)
- PostgreSQL self-hosted
- Cloudflare CDN (free)
- Supabase backup (free tier)

**Amaç:** Sistemi test et, iş modelini doğrula

### Faz 2: Growth (6-12 Ay) - **₺2,800/ay**

**Orta seviye pakete geç:**
- Azure App Service B2
- Azure SQL S1
- Managed services
- Monitoring + alerting

**Amaç:** Ölçeklen, güvenilirliği artır

### Faz 3: Scale (12+ Ay) - **Custom**

**İhtiyaca göre optimize et:**
- Load balancing ekle
- CDN premium
- Multi-region deployment
- Advanced security

---

## 💡 Maliyet Optimizasyon İpuçları

### 1. İkas API Limit Kontrolü
```csharp
// Rate limiting ile gereksiz çağrıları engelle
public class RateLimiter
{
    private static int _dailyCallCount = 0;
    private const int DAILY_LIMIT = 5000; // Günlük limit
    
    public static bool CanMakeCall()
    {
        if (_dailyCallCount >= DAILY_LIMIT)
        {
            Console.WriteLine("Daily API limit reached. Skipping...");
            return false;
        }
        _dailyCallCount++;
        return true;
    }
}
```

### 2. Incremental Sync (Sadece Değişenleri Gönder)
```csharp
// Ürün hash'ini karşılaştır, değişmediyse API çağrısı yapma
var productHash = GenerateHash(product);
var existingHash = GetStoredHash(product.Sku);

if (productHash == existingHash)
{
    skippedCount++;
    continue; // Skip API call
}

// API call yap
await ikasApiService.CreateOrUpdateProduct(product);
UpdateStoredHash(product.Sku, productHash);
```

### 3. Batch Processing
```csharp
// 100'er ürün grupla, paralel işle
var batches = products.Chunk(100);

await Parallel.ForEachAsync(batches, 
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    async (batch, ct) =>
    {
        foreach (var product in batch)
        {
            await ProcessProduct(product);
            await Task.Delay(100); // Rate limit protection
        }
    });
```

### 4. CDN Kullanımı
```
// Görselleri CDN'e taşı (direkt İkas'a gönderme)
Master Site (hobizubi.com) görselleri → Cloudflare CDN → İkas'a URL ver

Avantaj:
- Cloudflare CDN: Free, sınırsız bandwidth
- İkas'a sadece URL gönderiliyor (veri transfer yok)
- Hızlı görsel yükleme
```

### 5. Database Optimizasyonu
```sql
-- Eski transfer log'larını temizle (1 yıldan eski)
DELETE FROM transfer_logs 
WHERE start_date < DATEADD(YEAR, -1, GETDATE());

-- Index'leri optimize et
REBUILD INDEX ALL ON products;

-- Veritabanı boyutunu düşür
SHRINK DATABASE MultiSiteIkasDB;
```

### 6. Serverless Alternative (En Ekonomik)
```yaml
# AWS Lambda + DynamoDB
Function Memory: 512 MB
Execution Time: 30 saniye/ürün
Monthly Executions: 120,000 (4,000/gün × 30)

Cost Breakdown:
- Lambda: $0.0000166667/GB-second = $100/ay
- DynamoDB: $0.25/GB = $0.50/ay
- S3: $0.023/GB = $0.23/ay
Total: ~$101/ay (₺2,700/ay)

Avantaj:
✅ Sadece kullandığın kadar öde
✅ Otomatik ölçekleme
✅ Bakım yok
```

---

## 📈 ROI Hesaplama (10,000 Ürün Senaryosu)

### Varsayımlar
- Ortalama ürün fiyatı: ₺150
- Conversion rate: %2
- Site başına aylık ziyaretçi: 5,000
- Ek satış oranı (multi-site sayesinde): %15

### Gelir Tahmini

**Site Başına:**
- Aylık ziyaretçi: 5,000
- Satın alma: 5,000 × 2% = 100 sipariş/ay
- Aylık ciro: 100 × ₺150 = ₺15,000

**4 Site Toplamı:**
- Aylık ciro: ₺15,000 × 4 = **₺60,000/ay**
- Yıllık ciro: **₺720,000/yıl**

**Ek Gelir (Multi-site optimizasyonu):**
- %15 artış = ₺720,000 × 0.15 = **₺108,000/yıl**

### Kar Analizi

| Senaryo | Yıllık Maliyet | Ek Gelir | Net Kazanç | ROI |
|---------|----------------|----------|------------|-----|
| **Ekonomik** | ₺9,780 | ₺108,000 | ₺98,220 | **1,004%** |
| **Orta Seviye** | ₺33,600 | ₺108,000 | ₺74,400 | **221%** |
| **Enterprise** | ₺238,800 | ₺108,000 | -₺130,800 | **-55%** ❌ |

**Sonuç:** Ekonomik veya Orta Seviye paket ile başlamak en mantıklısı! 🎯

---

## 🚀 Önerilen Başlangıç Planı

### İlk Ay (Setup)
```
Maliyet: ₺815
- Hetzner Cloud sunucu kiralamaa (₺350)
- Domain + SSL (₺100)
- PostgreSQL setup (₺0)
- Cloudflare CDN (₺0)
- Geliştirme + test (₺365)
```

### 2-3. Ay (Beta Test)
```
Maliyet: ₺815/ay
- 1,000 ürünle test et
- 1 siteye başla (recinem.com)
- Hataları düzelt
- Performans optimizasyonu
```

### 4-6. Ay (Full Rollout)
```
Maliyet: ₺815/ay
- 10,000 ürün tam aktarım
- 4 siteye genişlet
- Monitoring + alerting ekle
- Günlük sync optimize et
```

### 7+ Ay (Optimization)
```
Maliyet: ₺815-2,800/ay (ihtiyaca göre)
- Eğer sistem stabil ise devam et
- Eğer yük artarsa orta seviyeye upgrade
- Gelir artışını takip et
- Gerekirse ek optimizasyonlar
```

---

## 📊 Sonuç ve Öneri

### 10,000 Ürün için Önerilen Stack

**Başlangıç (0-6 Ay):**
```
Platform: Hetzner Cloud
Stack: Node.js + PostgreSQL
Maliyet: ₺815/ay (₺9,780/yıl)
```

**Büyüme (6-18 Ay):**
```
Platform: Azure App Service
Stack: C# ASP.NET Core + Azure SQL
Maliyet: ₺2,800/ay (₺33,600/yıl)
```

**Scale (18+ Ay):**
```
Platform: Custom optimized
Stack: Hybrid (serverless + containers)
Maliyet: ₺5,000-8,000/ay (ihtiyaca göre)
```

### Kritik Başarı Faktörleri

✅ **İlk 3 ayda:** Sistemi çalışır hale getir (₺2,445 bütçe)  
✅ **İlk 6 ayda:** ROI'yi gör (₺54,000 ek gelir vs ₺4,890 maliyet)  
✅ **1 yılda:** Tam optimizasyon (₺108,000 ek gelir vs ₺9,780 maliyet)

**Net Kazanç (1. Yıl):** **₺98,220** 🎉

---

## 💼 Ek Maliyetler (Opsiyonel)

| Hizmet | Açıklama | Maliyet |
|--------|----------|---------|
| **Developer Time** | İlk setup + development | ₺15,000-30,000 (one-time) |
| **Maintenance** | Aylık bakım (2-4 saat/ay) | ₺2,000-4,000/ay |
| **Email Service** | SendGrid/SES (bildirimler için) | ₺50/ay |
| **Error Tracking** | Sentry.io (hata izleme) | ₺150/ay |
| **SSL Certificate** | Let's Encrypt (free) veya Wildcard | ₺0-500/yıl |
| **Domain** | 4 domain (eğer yeni alınacaksa) | ₺400/yıl |

---

Her şey dahil, **en ekonomik başlangıç için toplam bütçe: ₺815/ay (₺9,780/yıl)** 🚀

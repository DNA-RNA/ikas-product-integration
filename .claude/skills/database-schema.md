# Database Schema — SQL Server T-SQL

## Database Adı
`MultiSiteIkasDB`

## Tablolar

### 1. `companies` — Site/Firma Kayıtları
```sql
CREATE TABLE [dbo].[companies] (
    [id]                BIGINT PRIMARY KEY IDENTITY(1,1),
    [name]              NVARCHAR(255) NOT NULL,
    [email]             NVARCHAR(255),
    [website_url]       NVARCHAR(500),
    
    -- İkas API Credentials (production'da encrypted)
    [ikas_api_key]      NVARCHAR(500) NULL,     -- Hedef mağaza İkas credentials
    [ikas_api_secret]   NVARCHAR(500) NULL,    -- null = aktif olmayan site
    [language_code]     NVARCHAR(10) NULL,     -- 'tr', 'en' — mallofmolds için 'en'
    
    -- Status
    [is_active]         BIT NOT NULL DEFAULT 1,
    [created_date]      DATETIME NOT NULL DEFAULT GETDATE(),
    [updated_date]      DATETIME NULL
);
```

Seed data:
```sql
INSERT INTO companies (name, website_url, language_code, is_active) VALUES
    ('hobizubi.com', 'https://hobizubi.com', 'tr', 1),
    ('recinem.com', 'https://recinem.com', 'tr', 1),
    ('boncukpasaji.com', 'https://boncukpasaji.com', 'tr', 1),
    ('kalipatolyesi.com', 'https://kalipatolyesi.com', 'tr', 1),
    ('mallofmolds.com', 'https://mallofmolds.com', 'en', 1);
```

### 2. `xml_sources` — Master Mağaza XML Kaynakları
```sql
CREATE TABLE [dbo].[xml_sources] (
    [id]                    BIGINT PRIMARY KEY IDENTITY(1,1),
    [name]                  NVARCHAR(255) NOT NULL,
    [source_company_id]     BIGINT NOT NULL,
    [xml_url]               NVARCHAR(1000) NOT NULL,
    [is_active]             BIT NOT NULL DEFAULT 1,
    [sync_frequency_hours]  INT NOT NULL DEFAULT 24,
    [last_sync_date]        DATETIME NULL,
    [next_sync_date]        DATETIME NULL,
    [last_sync_status]      NVARCHAR(50) NULL,   -- 'Success', 'Failed', 'InProgress'
    [created_date]          DATETIME NOT NULL DEFAULT GETDATE(),
    [updated_date]          DATETIME NULL,

    CONSTRAINT FK_XmlSource_Company
        FOREIGN KEY ([source_company_id]) REFERENCES [companies]([id])
);

CREATE INDEX IX_XmlSources_SourceCompany ON [xml_sources]([source_company_id]);
CREATE INDEX IX_XmlSources_NextSync ON [xml_sources]([next_sync_date]) WHERE [is_active] = 1;
```

### 3. `site_mappings` — XML Source → Target Company Mapping + Filtre/Fiyat

> **Yapı:** Her XML source'dan her target company'e ürün göndermek için mapping ve filtering/pricing rules tutar.
> İkas API Credentials artık **companies** tablosunda saklanır (hedef company'ye ait).

```sql
CREATE TABLE [dbo].[site_mappings] (
    [id]                        BIGINT PRIMARY KEY IDENTITY(1,1),
    [xml_source_id]             BIGINT NOT NULL,
    [target_company_id]         BIGINT NOT NULL,

    -- Pricing Rules (target company özelinde)
    [price_margin_percentage]   DECIMAL(18,2) NOT NULL DEFAULT 0,
    [additional_price]          DECIMAL(18,2) NOT NULL DEFAULT 0,
    [currency_override]         NVARCHAR(10) NULL,   -- null = master'dan gelen kullanılır

    -- Filters & Mappings (JSON)
    [category_filters]          NVARCHAR(MAX) NULL,  -- JSON: ["Reçine", "Boncuk > *"]
    [category_mappings]         NVARCHAR(MAX) NULL,  -- JSON: {"Hobi > Reçine": "Resin"}
    [brand_mappings]            NVARCHAR(MAX) NULL,  -- JSON: {"EpoxyPro": "EpoxyPro EN"}

    -- Behavior
    [deactivate_zero_stock]     BIT NOT NULL DEFAULT 1,
    [send_images]               BIT NOT NULL DEFAULT 1,

    -- Schedule (per-mapping, opsiyonel; null ise global default)
    [sync_cron]                 NVARCHAR(50) NULL,   -- '30 2 * * *'

    -- Status
    [is_active]                 BIT NOT NULL DEFAULT 1,
    [created_date]              DATETIME NOT NULL DEFAULT GETDATE(),
    [updated_date]              DATETIME NULL,

    CONSTRAINT FK_SiteMapping_XmlSource
        FOREIGN KEY ([xml_source_id]) REFERENCES [xml_sources]([id]),
    CONSTRAINT FK_SiteMapping_TargetCompany
        FOREIGN KEY ([target_company_id]) REFERENCES [companies]([id]),
    CONSTRAINT UQ_SiteMapping UNIQUE ([xml_source_id], [target_company_id])
);

CREATE INDEX IX_SiteMapping_XmlSource ON [site_mappings]([xml_source_id]);
CREATE INDEX IX_SiteMapping_TargetCompany ON [site_mappings]([target_company_id]);
CREATE INDEX IX_SiteMapping_Active ON [site_mappings]([is_active]) WHERE [is_active] = 1;
```

### 4. `products` — Master XML'den Parse Edilmiş Ürünler

> **Yapı:** Her ürün master company (hobizubi.com) tarafından XML üzerinden gelmektedir.
> İlişki: company (master) → xml_sources → products (1 source → N product).
> SKU unique constraint: `(xml_source_id, sku)` — aynı SKU'yu iki kere gelmesi = update.

```sql
CREATE TABLE [dbo].[products] (
    [id]                BIGINT PRIMARY KEY IDENTITY(1,1),
    [company_id]        BIGINT NOT NULL,    -- master mağaza (hobizubi.com)
    [xml_source_id]     BIGINT NOT NULL,
    [external_id]       NVARCHAR(100) NULL, -- master mağaza internal id

    -- Product Details
    [sku]               NVARCHAR(255) NOT NULL,
    [barcode]           NVARCHAR(255) NULL,
    [name]              NVARCHAR(1000) NOT NULL,
    [description]       NVARCHAR(MAX) NULL,
    [category_path]     NVARCHAR(500) NOT NULL,
    [brand]             NVARCHAR(255) NULL,

    -- Pricing
    [original_price]    DECIMAL(18,2) NOT NULL,
    [sale_price]        DECIMAL(18,2) NOT NULL,
    [discount_price]    DECIMAL(18,2) NULL,
    [currency]          NVARCHAR(10) NOT NULL DEFAULT 'TRY',

    -- Inventory
    [stock_quantity]    INT NOT NULL DEFAULT 0,
    [weight]            DECIMAL(18,3) NULL,

    -- Extra
    [images_json]       NVARCHAR(MAX) NULL, -- JSON array of URLs
    [attributes_json]   NVARCHAR(MAX) NULL, -- JSON object {name: value}

    -- Status & Lifecycle
    [is_active]         BIT NOT NULL DEFAULT 1,    -- aktif ürün
    [is_deleted]        BIT NOT NULL DEFAULT 0,    -- master'da silindi (soft delete)
    [created_date]      DATETIME NOT NULL DEFAULT GETDATE(),
    [updated_date]      DATETIME NULL,
    [last_seen_date]    DATETIME NOT NULL DEFAULT GETDATE(), -- son XML'de görüldüğü tarih

    CONSTRAINT FK_Product_Company
        FOREIGN KEY ([company_id]) REFERENCES [companies]([id]),
    CONSTRAINT FK_Product_XmlSource
        FOREIGN KEY ([xml_source_id]) REFERENCES [xml_sources]([id]),
    CONSTRAINT UQ_Product UNIQUE ([xml_source_id], [sku])
);

CREATE INDEX IX_Products_Sku ON [products]([sku]);
CREATE INDEX IX_Products_Company ON [products]([company_id]);
CREATE INDEX IX_Products_XmlSource ON [products]([xml_source_id]);
CREATE INDEX IX_Products_Category ON [products]([category_path]);
CREATE INDEX IX_Products_IsDeleted ON [products]([is_deleted]) WHERE [is_deleted] = 1;
```

### 5. `product_transfers` — Hangi Ürün Hangi Hedefe
```sql
CREATE TABLE [dbo].[product_transfers] (
    [id]                    BIGINT PRIMARY KEY IDENTITY(1,1),
    [source_product_id]     BIGINT NOT NULL,
    [target_company_id]     BIGINT NOT NULL,
    [site_mapping_id]       BIGINT NOT NULL,

    -- İkas Tarafı
    [ikas_product_id]       NVARCHAR(255) NULL,
    [ikas_variant_id]       NVARCHAR(255) NULL,
    [target_sku]            NVARCHAR(255) NULL, -- master sku ile aynı ama log için sakla

    -- Transfer Details
    [transferred_price]     DECIMAL(18,2) NULL,
    [transferred_category]  NVARCHAR(500) NULL,
    [transfer_status]       TINYINT NOT NULL DEFAULT 0,  -- 0=Pending, 1=Success, 2=Failed, 3=Skipped
    [error_message]         NVARCHAR(MAX) NULL,
    [retry_count]           INT NOT NULL DEFAULT 0,

    -- Dates
    [first_transfer_date]   DATETIME NULL,
    [last_transfer_date]    DATETIME NULL,
    [created_date]          DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_ProductTransfer_SourceProduct
        FOREIGN KEY ([source_product_id]) REFERENCES [products]([id]),
    CONSTRAINT FK_ProductTransfer_TargetCompany
        FOREIGN KEY ([target_company_id]) REFERENCES [companies]([id]),
    CONSTRAINT FK_ProductTransfer_SiteMapping
        FOREIGN KEY ([site_mapping_id]) REFERENCES [site_mappings]([id]),
    CONSTRAINT UQ_ProductTransfer UNIQUE ([source_product_id], [target_company_id])
);

CREATE INDEX IX_ProductTransfers_Source ON [product_transfers]([source_product_id]);
CREATE INDEX IX_ProductTransfers_Target ON [product_transfers]([target_company_id]);
CREATE INDEX IX_ProductTransfers_Status ON [product_transfers]([transfer_status]);
CREATE INDEX IX_ProductTransfers_IkasId ON [product_transfers]([ikas_product_id]) WHERE [ikas_product_id] IS NOT NULL;
```

### 6. `transfer_logs` — Job Çalışma Geçmişi
```sql
CREATE TABLE [dbo].[transfer_logs] (
    [id]                        BIGINT PRIMARY KEY IDENTITY(1,1),
    [xml_source_id]             BIGINT NOT NULL,
    [site_mapping_id]           BIGINT NOT NULL,
    [target_company_id]         BIGINT NOT NULL,
    [job_type]                  NVARCHAR(50) NOT NULL, -- 'XmlPull', 'Transfer', 'HealthCheck'
    [hangfire_job_id]           NVARCHAR(100) NULL,

    -- Stats
    [total_products_in_xml]     INT NOT NULL DEFAULT 0,
    [filtered_products_count]   INT NOT NULL DEFAULT 0,
    [success_count]             INT NOT NULL DEFAULT 0,
    [error_count]               INT NOT NULL DEFAULT 0,
    [skipped_count]             INT NOT NULL DEFAULT 0,

    -- Timing
    [start_date]                DATETIME NOT NULL,
    [end_date]                  DATETIME NULL,
    [duration_seconds]          INT NULL,

    -- Status
    [status]                    TINYINT NOT NULL DEFAULT 1, -- 1=InProgress, 2=Success, 3=Failed, 4=Cancelled
    [error_details]             NVARCHAR(MAX) NULL, -- JSON array of {sku, message}

    CONSTRAINT FK_TransferLog_XmlSource
        FOREIGN KEY ([xml_source_id]) REFERENCES [xml_sources]([id]),
    CONSTRAINT FK_TransferLog_SiteMapping
        FOREIGN KEY ([site_mapping_id]) REFERENCES [site_mappings]([id]),
    CONSTRAINT FK_TransferLog_TargetCompany
        FOREIGN KEY ([target_company_id]) REFERENCES [companies]([id])
);

CREATE INDEX IX_TransferLogs_StartDate ON [transfer_logs]([start_date] DESC);
CREATE INDEX IX_TransferLogs_Status ON [transfer_logs]([status]);
CREATE INDEX IX_TransferLogs_Mapping ON [transfer_logs]([site_mapping_id], [start_date] DESC);
```

## Enums (C# Tarafı)

```csharp
public enum TransferStatus : byte
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Skipped = 3
}

public enum TransferLogStatus : byte
{
    InProgress = 1,
    Success = 2,
    Failed = 3,
    Cancelled = 4
}
```

## Performans Notları

- `products` tablosu büyür (10K-100K ürün) → composite index `(xml_source_id, sku)` zaten unique constraint olarak var.
- `transfer_logs` zamanla şişer → 90 günden eski log'ları silen cleanup job düşün.
- `product_transfers.error_message` çok uzun olabilir → NVARCHAR(MAX) seçildi.
- `site_mappings.category_filters` JSON parsing her sync'te bir kere yapılır, cache'e gerek yok.

## Migration Stratejisi

1. **InitialCreate** migration: tüm tabloları yarat.
2. **SeedCompanies** migration: companies seed data.
3. Bundan sonra her şema değişikliği için ayrı migration.

```bash
dotnet ef migrations add InitialCreate \
  --project MultiSiteIkas.Data \
  --startup-project MultiSiteIkas.API
```

## Backup Stratejisi (Production)

- Full backup: günlük
- Differential: 6 saatte bir
- Transaction log: 15 dakikada bir
- Retention: 30 gün

API key/secret içerdiği için backup'lar da şifreli olmalı.

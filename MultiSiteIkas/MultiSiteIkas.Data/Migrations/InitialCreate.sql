-- InitialCreate Migration — PostgreSQL
-- MultiSiteIkas veritabanı: DBeaver veya psql ile MultiSiteIkasDB üzerinde çalıştırın.

-- 1. companies
CREATE TABLE IF NOT EXISTS companies (
    id              BIGSERIAL PRIMARY KEY,
    name            VARCHAR(255) NOT NULL,
    email           VARCHAR(255),
    website_url     VARCHAR(500),
    ikas_api_key    VARCHAR(500),
    ikas_api_secret VARCHAR(500),
    language_code   VARCHAR(10),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_date    TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_date    TIMESTAMP
);

-- 2. xml_sources
CREATE TABLE IF NOT EXISTS xml_sources (
    id                   BIGSERIAL PRIMARY KEY,
    name                 VARCHAR(255) NOT NULL,
    source_company_id    BIGINT NOT NULL REFERENCES companies(id),
    xml_url              VARCHAR(1000) NOT NULL,
    is_active            BOOLEAN NOT NULL DEFAULT TRUE,
    sync_frequency_hours INTEGER NOT NULL DEFAULT 24,
    last_sync_date       TIMESTAMP,
    next_sync_date       TIMESTAMP,
    last_sync_status     VARCHAR(50),
    created_date         TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_date         TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_xml_sources_source_company ON xml_sources(source_company_id);
CREATE INDEX IF NOT EXISTS ix_xml_sources_next_sync ON xml_sources(next_sync_date) WHERE is_active = TRUE;

-- 3. site_mappings
CREATE TABLE IF NOT EXISTS site_mappings (
    id                      BIGSERIAL PRIMARY KEY,
    xml_source_id           BIGINT NOT NULL REFERENCES xml_sources(id),
    target_company_id       BIGINT NOT NULL REFERENCES companies(id),
    price_margin_percentage NUMERIC(18,2) NOT NULL DEFAULT 0,
    additional_price        NUMERIC(18,2) NOT NULL DEFAULT 0,
    currency_override       VARCHAR(10),
    category_filters        TEXT,
    category_mappings       TEXT,
    brand_mappings          TEXT,
    deactivate_zero_stock   BOOLEAN NOT NULL DEFAULT TRUE,
    send_images             BOOLEAN NOT NULL DEFAULT TRUE,
    sync_cron               VARCHAR(50),
    is_active               BOOLEAN NOT NULL DEFAULT TRUE,
    created_date            TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_date            TIMESTAMP,
    UNIQUE (xml_source_id, target_company_id)
);

CREATE INDEX IF NOT EXISTS ix_site_mapping_xml_source     ON site_mappings(xml_source_id);
CREATE INDEX IF NOT EXISTS ix_site_mapping_target_company ON site_mappings(target_company_id);
CREATE INDEX IF NOT EXISTS ix_site_mapping_active         ON site_mappings(is_active) WHERE is_active = TRUE;

-- 4. products
CREATE TABLE IF NOT EXISTS products (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES companies(id),
    xml_source_id   BIGINT NOT NULL REFERENCES xml_sources(id),
    external_id     VARCHAR(100),
    sku             VARCHAR(255) NOT NULL,
    barcode         VARCHAR(255),
    name            VARCHAR(1000) NOT NULL,
    description     TEXT,
    category_path   VARCHAR(500) NOT NULL,
    brand           VARCHAR(255),
    original_price  NUMERIC(18,2) NOT NULL,
    sale_price      NUMERIC(18,2) NOT NULL,
    discount_price  NUMERIC(18,2),
    currency        VARCHAR(10) NOT NULL DEFAULT 'TRY',
    stock_quantity  INTEGER NOT NULL DEFAULT 0,
    weight          NUMERIC(18,3),
    images_json     TEXT,
    attributes_json TEXT,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    created_date    TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_date    TIMESTAMP,
    last_seen_date  TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE (xml_source_id, sku)
);

CREATE INDEX IF NOT EXISTS ix_products_sku        ON products(sku);
CREATE INDEX IF NOT EXISTS ix_products_company    ON products(company_id);
CREATE INDEX IF NOT EXISTS ix_products_xml_source ON products(xml_source_id);
CREATE INDEX IF NOT EXISTS ix_products_category   ON products(category_path);
CREATE INDEX IF NOT EXISTS ix_products_is_deleted ON products(is_deleted) WHERE is_deleted = TRUE;

-- 5. product_transfers
CREATE TABLE IF NOT EXISTS product_transfers (
    id                   BIGSERIAL PRIMARY KEY,
    source_product_id    BIGINT NOT NULL REFERENCES products(id),
    target_company_id    BIGINT NOT NULL REFERENCES companies(id),
    site_mapping_id      BIGINT NOT NULL REFERENCES site_mappings(id),
    ikas_product_id      VARCHAR(255),
    ikas_variant_id      VARCHAR(255),
    target_sku           VARCHAR(255),
    transferred_price    NUMERIC(18,2),
    transferred_category VARCHAR(500),
    transfer_status      SMALLINT NOT NULL DEFAULT 0,
    error_message        TEXT,
    retry_count          INTEGER NOT NULL DEFAULT 0,
    first_transfer_date  TIMESTAMP,
    last_transfer_date   TIMESTAMP,
    created_date         TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE (source_product_id, target_company_id)
);

CREATE INDEX IF NOT EXISTS ix_product_transfers_source  ON product_transfers(source_product_id);
CREATE INDEX IF NOT EXISTS ix_product_transfers_target  ON product_transfers(target_company_id);
CREATE INDEX IF NOT EXISTS ix_product_transfers_status  ON product_transfers(transfer_status);
CREATE INDEX IF NOT EXISTS ix_product_transfers_ikas_id ON product_transfers(ikas_product_id) WHERE ikas_product_id IS NOT NULL;

-- 6. transfer_logs
CREATE TABLE IF NOT EXISTS transfer_logs (
    id                       BIGSERIAL PRIMARY KEY,
    xml_source_id            BIGINT NOT NULL REFERENCES xml_sources(id),
    site_mapping_id          BIGINT NOT NULL REFERENCES site_mappings(id),
    target_company_id        BIGINT NOT NULL REFERENCES companies(id),
    job_type                 VARCHAR(50) NOT NULL,
    hangfire_job_id          VARCHAR(100),
    start_date               TIMESTAMP NOT NULL,
    end_date                 TIMESTAMP,
    duration_ms              BIGINT,
    total_products_processed INTEGER NOT NULL DEFAULT 0,
    success_count            INTEGER NOT NULL DEFAULT 0,
    failed_count             INTEGER NOT NULL DEFAULT 0,
    skipped_count            INTEGER NOT NULL DEFAULT 0,
    status                   SMALLINT NOT NULL DEFAULT 0,
    error_message            TEXT,
    stack_trace              TEXT,
    detail_json              TEXT,
    created_date             TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_transfer_logs_start_date ON transfer_logs(start_date DESC);
CREATE INDEX IF NOT EXISTS ix_transfer_logs_status     ON transfer_logs(status);
CREATE INDEX IF NOT EXISTS ix_transfer_logs_mapping    ON transfer_logs(site_mapping_id, start_date DESC);

-- Seed: companies (idempotent)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM companies WHERE website_url = 'https://hobizubi.com') THEN
        INSERT INTO companies (name, website_url, language_code, is_active) VALUES
            ('hobizubi.com',      'https://hobizubi.com',      'tr', TRUE),
            ('recinem.com',       'https://recinem.com',       'tr', TRUE),
            ('boncukpasaji.com',  'https://boncukpasaji.com',  'tr', TRUE),
            ('kalipatolyesi.com', 'https://kalipatolyesi.com', 'tr', TRUE),
            ('mallofmolds.com',   'https://mallofmolds.com',   'en', TRUE);
    END IF;
END;
$$;

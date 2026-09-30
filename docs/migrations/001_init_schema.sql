-- Migration 001: Tam veritabanı şeması
-- Hangfire tabloları Worker ilk çalıştığında otomatik oluşturulur (PrepareSchemaIfNecessary = true)

-- ── Companies ──────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS companies (
    id              BIGSERIAL     PRIMARY KEY,
    name            VARCHAR(200)  NOT NULL,
    email           VARCHAR(200),
    website_url     VARCHAR(500),
    ikas_api_key    VARCHAR(500),
    ikas_api_secret VARCHAR(500),
    language_code   VARCHAR(10)   NOT NULL DEFAULT 'tr',
    is_active       BOOLEAN       NOT NULL DEFAULT TRUE,
    created_date    TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_date    TIMESTAMPTZ
);

-- ── XML Sources ────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS xml_sources (
    id                   BIGSERIAL     PRIMARY KEY,
    name                 VARCHAR(200)  NOT NULL,
    source_company_id    BIGINT        NOT NULL REFERENCES companies(id),
    xml_url              VARCHAR(1000) NOT NULL,
    is_active            BOOLEAN       NOT NULL DEFAULT TRUE,
    sync_frequency_hours INTEGER       NOT NULL DEFAULT 24,
    last_sync_date       TIMESTAMPTZ,
    next_sync_date       TIMESTAMPTZ,
    last_sync_status     VARCHAR(50),
    created_date         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_date         TIMESTAMPTZ
);

-- ── Site Mappings ──────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS site_mappings (
    id                       BIGSERIAL      PRIMARY KEY,
    xml_source_id            BIGINT         NOT NULL REFERENCES xml_sources(id),
    target_company_id        BIGINT         NOT NULL REFERENCES companies(id),
    price_margin_percentage  DECIMAL(10,4)  NOT NULL DEFAULT 0,
    additional_price         DECIMAL(10,4)  NOT NULL DEFAULT 0,
    currency_override        VARCHAR(10),
    category_filters         TEXT,
    category_mappings        TEXT,
    brand_mappings           TEXT,
    deactivate_zero_stock    BOOLEAN        NOT NULL DEFAULT TRUE,
    send_images              BOOLEAN        NOT NULL DEFAULT TRUE,
    sync_cron                VARCHAR(100),
    is_active                BOOLEAN        NOT NULL DEFAULT TRUE,
    created_date             TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    updated_date             TIMESTAMPTZ
);

-- ── Products ───────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS products (
    id              BIGSERIAL      PRIMARY KEY,
    company_id      BIGINT         NOT NULL REFERENCES companies(id),
    xml_source_id   BIGINT         NOT NULL REFERENCES xml_sources(id),
    external_id     VARCHAR(200),
    sku             VARCHAR(200)   NOT NULL,
    barcode         VARCHAR(200),
    name            TEXT           NOT NULL,
    description     TEXT,
    category_path   TEXT           NOT NULL DEFAULT '',
    brand           VARCHAR(200),
    original_price  DECIMAL(12,4)  NOT NULL DEFAULT 0,
    sale_price      DECIMAL(12,4)  NOT NULL DEFAULT 0,
    discount_price  DECIMAL(12,4),
    currency        VARCHAR(10)    NOT NULL DEFAULT 'TRY',
    stock_quantity  INTEGER        NOT NULL DEFAULT 0,
    weight          DECIMAL(10,4),
    images_json     TEXT,
    attributes_json TEXT,
    is_active       BOOLEAN        NOT NULL DEFAULT TRUE,
    is_deleted      BOOLEAN        NOT NULL DEFAULT FALSE,
    created_date    TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    updated_date    TIMESTAMPTZ,
    last_seen_date  TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    UNIQUE (xml_source_id, sku)
);

CREATE INDEX IF NOT EXISTS idx_products_xml_source_id  ON products(xml_source_id);
CREATE INDEX IF NOT EXISTS idx_products_company_id     ON products(company_id);
CREATE INDEX IF NOT EXISTS idx_products_category_path  ON products(category_path);
CREATE INDEX IF NOT EXISTS idx_products_is_deleted     ON products(is_deleted) WHERE is_deleted = FALSE;

-- ── Product Transfers ──────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS product_transfers (
    id                   BIGSERIAL      PRIMARY KEY,
    source_product_id    BIGINT         NOT NULL REFERENCES products(id),
    target_company_id    BIGINT         NOT NULL REFERENCES companies(id),
    site_mapping_id      BIGINT         NOT NULL REFERENCES site_mappings(id),
    ikas_product_id      VARCHAR(200),
    ikas_variant_id      VARCHAR(200),
    target_sku           VARCHAR(200),
    transferred_price    DECIMAL(12,4),
    transferred_category VARCHAR(500),
    transfer_status      SMALLINT       NOT NULL DEFAULT 0, -- 0=Pending 1=Success 2=Failed 3=Skipped
    error_message        TEXT,
    retry_count          INTEGER        NOT NULL DEFAULT 0,
    first_transfer_date  TIMESTAMPTZ,
    last_transfer_date   TIMESTAMPTZ,
    created_date         TIMESTAMPTZ    NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_pt_target_company_id   ON product_transfers(target_company_id);
CREATE INDEX IF NOT EXISTS idx_pt_source_product_id   ON product_transfers(source_product_id);
CREATE INDEX IF NOT EXISTS idx_pt_site_mapping_id     ON product_transfers(site_mapping_id);
CREATE INDEX IF NOT EXISTS idx_pt_transfer_status     ON product_transfers(transfer_status);

-- ── Transfer Logs ──────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS transfer_logs (
    id                       BIGSERIAL   PRIMARY KEY,
    xml_source_id            BIGINT      REFERENCES xml_sources(id),
    site_mapping_id          BIGINT      REFERENCES site_mappings(id),
    target_company_id        BIGINT      REFERENCES companies(id),
    job_type                 VARCHAR(50) NOT NULL,
    hangfire_job_id          VARCHAR(200),
    start_date               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    end_date                 TIMESTAMPTZ,
    duration_ms              BIGINT,
    total_products_processed INTEGER     NOT NULL DEFAULT 0,
    success_count            INTEGER     NOT NULL DEFAULT 0,
    failed_count             INTEGER     NOT NULL DEFAULT 0,
    skipped_count            INTEGER     NOT NULL DEFAULT 0,
    status                   SMALLINT    NOT NULL DEFAULT 0, -- 0=Pending 1=Success 2=Failed
    error_message            TEXT,
    stack_trace              TEXT,
    detail_json              TEXT,
    created_date             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tl_target_company_id ON transfer_logs(target_company_id);
CREATE INDEX IF NOT EXISTS idx_tl_site_mapping_id   ON transfer_logs(site_mapping_id);
CREATE INDEX IF NOT EXISTS idx_tl_start_date        ON transfer_logs(start_date DESC);
CREATE INDEX IF NOT EXISTS idx_tl_status            ON transfer_logs(status);

-- ── App Users ──────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS app_users (
    id            BIGSERIAL    PRIMARY KEY,
    username      VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role          VARCHAR(20)  NOT NULL DEFAULT 'customer',
    company_id    BIGINT       REFERENCES companies(id) ON DELETE SET NULL,
    is_active     BOOLEAN      NOT NULL DEFAULT TRUE,
    created_date  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_date  TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_app_users_username   ON app_users(username);
CREATE INDEX IF NOT EXISTS idx_app_users_company_id ON app_users(company_id) WHERE company_id IS NOT NULL;

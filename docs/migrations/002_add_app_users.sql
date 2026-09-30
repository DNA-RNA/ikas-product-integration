-- Migration 002: app_users tablosu
-- Çalıştırmak için: psql -U ranayalcin -d postgres -f 002_add_app_users.sql

CREATE TABLE IF NOT EXISTS app_users (
    id            BIGSERIAL    PRIMARY KEY,
    username      VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role          VARCHAR(20)  NOT NULL DEFAULT 'customer', -- 'admin' | 'customer'
    company_id    BIGINT       REFERENCES companies(id) ON DELETE SET NULL,
    is_active     BOOLEAN      NOT NULL DEFAULT TRUE,
    created_date  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_date  TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_app_users_username   ON app_users(username);
CREATE INDEX IF NOT EXISTS idx_app_users_company_id ON app_users(company_id) WHERE company_id IS NOT NULL;

-- İlk admin kullanıcı API ilk ayağa kalktığında otomatik oluşturulur (Program.cs → SeedAdminAsync).
-- Varsayılan: admin / Admin123!  — production öncesi mutlaka değiştir!

using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class SiteMappingRepository : ISiteMappingRepository
{
    private readonly IDbConnectionFactory _factory;

    public SiteMappingRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<SiteMapping?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<SiteMapping>(
            "SELECT * FROM site_mappings WHERE id = @id", new { id });
    }

    public async Task<IEnumerable<SiteMapping>> GetByXmlSourceIdAsync(long xmlSourceId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<SiteMapping>(
            "SELECT * FROM site_mappings WHERE xml_source_id = @xmlSourceId AND is_active = TRUE",
            new { xmlSourceId });
    }

    public async Task<IEnumerable<SiteMapping>> GetByTargetCompanyIdAsync(long targetCompanyId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<SiteMapping>(
            "SELECT * FROM site_mappings WHERE target_company_id = @targetCompanyId AND is_active = TRUE",
            new { targetCompanyId });
    }

    public async Task<IEnumerable<SiteMapping>> GetAllActiveAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<SiteMapping>(
            "SELECT * FROM site_mappings WHERE is_active = TRUE");
    }

    public async Task<SiteMapping?> GetBySourceAndTargetAsync(long xmlSourceId, long targetCompanyId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<SiteMapping>(
            "SELECT * FROM site_mappings WHERE xml_source_id = @xmlSourceId AND target_company_id = @targetCompanyId",
            new { xmlSourceId, targetCompanyId });
    }

    public async Task<long> CreateAsync(SiteMapping siteMapping, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO site_mappings
                (xml_source_id, target_company_id, price_margin_percentage, additional_price,
                 currency_override, category_filters, category_mappings, brand_mappings,
                 deactivate_zero_stock, send_images, sync_cron, is_active, created_date)
            VALUES
                (@XmlSourceId, @TargetCompanyId, @PriceMarginPercentage, @AdditionalPrice,
                 @CurrencyOverride, @CategoryFilters, @CategoryMappings, @BrandMappings,
                 @DeactivateZeroStock, @SendImages, @SyncCron, @IsActive, NOW())
            RETURNING id
            """;
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, siteMapping);
    }

    public async Task<bool> UpdateAsync(SiteMapping siteMapping, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE site_mappings SET
                price_margin_percentage = @PriceMarginPercentage,
                additional_price        = @AdditionalPrice,
                currency_override       = @CurrencyOverride,
                category_filters        = @CategoryFilters,
                category_mappings       = @CategoryMappings,
                brand_mappings          = @BrandMappings,
                deactivate_zero_stock   = @DeactivateZeroStock,
                send_images             = @SendImages,
                sync_cron               = @SyncCron,
                is_active               = @IsActive,
                updated_date            = NOW()
            WHERE id = @Id
            """;
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(sql, siteMapping) > 0;
    }

    public async Task<bool> UpdateFiltersAsync(long id, string? categoryFilters, string? categoryMappings, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE site_mappings SET category_filters = @categoryFilters, category_mappings = @categoryMappings, updated_date = NOW() WHERE id = @id",
            new { id, categoryFilters, categoryMappings }) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "DELETE FROM site_mappings WHERE id = @id", new { id }) > 0;
    }
}

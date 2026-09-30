using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class SiteMappingRepository(IDbConnectionFactory factory) : ISiteMappingRepository
{
    public async Task<SiteMapping?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<SiteMapping>(
            "SELECT * FROM site_mappings WHERE id = @id", new { id });
    }

    public async Task<(IEnumerable<SiteMapping> Items, long TotalCount)> GetPagedAsync(
        int page, int pageSize, long? targetCompanyId = null, bool? isActive = null,
        string? sortField = null, string? sortDir = null, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 10;
        var offset = (page - 1) * pageSize;

        string orderColumn = sortField?.ToLower() switch
        {
            "id"                      => "id",
            "createdate"              => "created_date",
            "pricemarginpercentage"   => "price_margin_percentage",
            "isactive"                => "is_active",
            _                         => "created_date"
        };
        string direction = sortDir?.ToLower() == "asc" ? "ASC" : "DESC";

        var where = "1=1";
        if (targetCompanyId.HasValue) where += " AND target_company_id = @targetCompanyId";
        if (isActive.HasValue)        where += " AND is_active = @isActive";

        using var conn = factory.CreateConnection();
        var multi = await conn.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM site_mappings WHERE {where};
            SELECT * FROM site_mappings WHERE {where} ORDER BY {orderColumn} {direction} LIMIT @pageSize OFFSET @offset
            """, new { targetCompanyId, isActive, pageSize, offset });

        var total = await multi.ReadSingleAsync<long>();
        var items = (await multi.ReadAsync<SiteMapping>()).ToList();

        return (items, total);
    }

    public async Task<IEnumerable<SiteMapping>> GetAllActiveAsync(CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QueryAsync<SiteMapping>("SELECT * FROM site_mappings WHERE is_active = TRUE");
    }

    public async Task<IEnumerable<SiteMapping>> GetByXmlSourceIdAsync(long xmlSourceId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QueryAsync<SiteMapping>(
            "SELECT * FROM site_mappings WHERE xml_source_id = @xmlSourceId AND is_active = TRUE",
            new { xmlSourceId });
    }

    public async Task<SiteMapping?> GetBySourceAndTargetAsync(long xmlSourceId, long targetCompanyId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
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
        using var conn = factory.CreateConnection();
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
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync(sql, siteMapping) > 0;
    }

    public async Task<bool> UpdateFiltersAsync(long id, string? categoryFilters, string? categoryMappings, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE site_mappings SET category_filters = @categoryFilters, category_mappings = @categoryMappings, updated_date = NOW() WHERE id = @id",
            new { id, categoryFilters, categoryMappings }) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM site_mappings WHERE id = @id", new { id }) > 0;
    }
}

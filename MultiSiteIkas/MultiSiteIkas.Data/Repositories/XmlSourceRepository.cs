using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class XmlSourceRepository(IDbConnectionFactory factory) : IXmlSourceRepository
{
    public async Task<XmlSource?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<XmlSource>(
            "SELECT * FROM xml_sources WHERE id = @id", new { id });
    }

    public async Task<(IEnumerable<XmlSource> Items, long TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search = null, bool? isActive = null,
        string? sortField = null, string? sortDir = null, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 20;
        var offset = (page - 1) * pageSize;

        string orderColumn = sortField?.ToLower() switch
        {
            "name"               => "name",
            "lastsyncdate"       => "last_sync_date",
            "nextsyncdate"       => "next_sync_date",
            "syncfrequencyhours" => "sync_frequency_hours",
            _                    => "created_date"
        };
        string direction = sortDir?.ToLower() == "asc" ? "ASC" : "DESC";

        var where = "1=1";
        if (!string.IsNullOrWhiteSpace(search)) where += " AND name ILIKE @searchLike";
        if (isActive.HasValue)                  where += " AND is_active = @isActive";

        var searchLike = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

        using var conn = factory.CreateConnection();
        var multi = await conn.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM xml_sources WHERE {where};
            SELECT * FROM xml_sources WHERE {where} ORDER BY {orderColumn} {direction} LIMIT @pageSize OFFSET @offset
            """, new { searchLike, isActive, pageSize, offset });

        var total = await multi.ReadSingleAsync<long>();
        var items = (await multi.ReadAsync<XmlSource>()).ToList();
        return (items, total);
    }

    public async Task<IEnumerable<XmlSource>> GetActiveAsync(CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QueryAsync<XmlSource>("SELECT * FROM xml_sources WHERE is_active = TRUE");
    }

    public async Task<IEnumerable<XmlSource>> GetDueForSyncAsync(CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QueryAsync<XmlSource>(
            "SELECT * FROM xml_sources WHERE is_active = TRUE AND (next_sync_date IS NULL OR next_sync_date <= NOW())");
    }

    public async Task<long> CreateAsync(XmlSource xmlSource, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO xml_sources (name, source_company_id, xml_url, is_active, sync_frequency_hours, created_date)
            VALUES (@Name, @SourceCompanyId, @XmlUrl, @IsActive, @SyncFrequencyHours, NOW())
            RETURNING id
            """;
        using var conn = factory.CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, xmlSource);
    }

    public async Task<bool> UpdateAsync(XmlSource xmlSource, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE xml_sources SET
                name                 = @Name,
                xml_url              = @XmlUrl,
                is_active            = @IsActive,
                sync_frequency_hours = @SyncFrequencyHours,
                updated_date         = NOW()
            WHERE id = @Id
            """;
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync(sql, xmlSource) > 0;
    }

    public async Task<bool> UpdateSyncStatusAsync(long id, string status, DateTime nextSyncDate, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE xml_sources SET last_sync_date = NOW(), last_sync_status = @status, next_sync_date = @nextSyncDate, updated_date = NOW() WHERE id = @id",
            new { id, status, nextSyncDate }) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM xml_sources WHERE id = @id", new { id }) > 0;
    }
}
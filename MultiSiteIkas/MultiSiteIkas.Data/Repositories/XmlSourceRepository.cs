using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class XmlSourceRepository : IXmlSourceRepository
{
    private readonly IDbConnectionFactory _factory;

    public XmlSourceRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<XmlSource?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<XmlSource>(
            "SELECT * FROM xml_sources WHERE id = @id", new { id });
    }

    public async Task<IEnumerable<XmlSource>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<XmlSource>("SELECT * FROM xml_sources");
    }

    public async Task<IEnumerable<XmlSource>> GetActiveAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<XmlSource>(
            "SELECT * FROM xml_sources WHERE is_active = TRUE");
    }

    public async Task<IEnumerable<XmlSource>> GetDueForSyncAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
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
        using var conn = _factory.CreateConnection();
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
                last_sync_date       = @LastSyncDate,
                next_sync_date       = @NextSyncDate,
                last_sync_status     = @LastSyncStatus,
                updated_date         = NOW()
            WHERE id = @Id
            """;
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(sql, xmlSource) > 0;
    }

    public async Task<bool> UpdateSyncStatusAsync(long id, string status, DateTime nextSyncDate, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE xml_sources SET last_sync_date = NOW(), last_sync_status = @status, next_sync_date = @nextSyncDate, updated_date = NOW() WHERE id = @id",
            new { id, status, nextSyncDate }) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "DELETE FROM xml_sources WHERE id = @id", new { id }) > 0;
    }
}

using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class TransferLogRepository : ITransferLogRepository
{
    private readonly IDbConnectionFactory _factory;

    public TransferLogRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<TransferLog?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<TransferLog>(
            "SELECT * FROM transfer_logs WHERE id = @id", new { id });
    }

    public async Task<IEnumerable<TransferLog>> GetByXmlSourceIdAsync(long xmlSourceId, int take = 50, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<TransferLog>(
            "SELECT * FROM transfer_logs WHERE xml_source_id = @xmlSourceId ORDER BY start_date DESC LIMIT @take",
            new { xmlSourceId, take });
    }

    public async Task<IEnumerable<TransferLog>> GetBySiteMappingIdAsync(long siteMappingId, int take = 50, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<TransferLog>(
            "SELECT * FROM transfer_logs WHERE site_mapping_id = @siteMappingId ORDER BY start_date DESC LIMIT @take",
            new { siteMappingId, take });
    }

    public async Task<IEnumerable<TransferLog>> GetByJobTypeAsync(string jobType, int take = 100, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<TransferLog>(
            "SELECT * FROM transfer_logs WHERE job_type = @jobType ORDER BY start_date DESC LIMIT @take",
            new { jobType, take });
    }

    public async Task<IEnumerable<TransferLog>> GetRecentFailuresAsync(int take = 50, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<TransferLog>(
            "SELECT * FROM transfer_logs WHERE status = 2 ORDER BY start_date DESC LIMIT @take",
            new { take });
    }

    public async Task<long> CreateAsync(TransferLog log, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO transfer_logs
                (xml_source_id, site_mapping_id, target_company_id, job_type, hangfire_job_id,
                 start_date, status, created_date)
            VALUES
                (@XmlSourceId, @SiteMappingId, @TargetCompanyId, @JobType, @HangfireJobId,
                 @StartDate, @Status, NOW())
            RETURNING id
            """;
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, log);
    }

    public async Task<bool> UpdateAsync(TransferLog log, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE transfer_logs SET
                end_date                 = @EndDate,
                duration_ms              = @DurationMs,
                total_products_processed = @TotalProductsProcessed,
                success_count            = @SuccessCount,
                failed_count             = @FailedCount,
                skipped_count            = @SkippedCount,
                status                   = @Status,
                error_message            = @ErrorMessage,
                stack_trace              = @StackTrace,
                detail_json              = @DetailJson
            WHERE id = @Id
            """;
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(sql, log) > 0;
    }

    public async Task<bool> UpdateStatusAsync(long id, byte status, long? durationMs, string? errorMessage = null, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE transfer_logs SET status = @status, end_date = NOW(), duration_ms = @durationMs, error_message = @errorMessage WHERE id = @id",
            new { id, status, durationMs, errorMessage }) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "DELETE FROM transfer_logs WHERE id = @id", new { id }) > 0;
    }
}

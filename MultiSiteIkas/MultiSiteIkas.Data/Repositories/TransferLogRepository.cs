using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class TransferLogRepository(IDbConnectionFactory factory) : ITransferLogRepository
{
    public async Task<TransferLog?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<TransferLog>(
            "SELECT * FROM transfer_logs WHERE id = @id", new { id });
    }

    public async Task<(IEnumerable<TransferLog> Items, long TotalCount)> GetPagedAsync(
        int page, int pageSize, long? siteMappingId = null, long? xmlSourceId = null,
        long? targetCompanyId = null, byte? status = null,
        string? sortField = null, string? sortDir = null, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 20;
        var offset = (page - 1) * pageSize;

        string orderColumn = sortField?.ToLower() switch
        {
            "startdate"    => "start_date",
            "enddate"      => "end_date",
            "durationms"   => "duration_ms",
            "successcount" => "success_count",
            "failedcount"  => "failed_count",
            "status"       => "status",
            _              => "start_date"
        };
        string direction = sortDir?.ToLower() == "asc" ? "ASC" : "DESC";

        var where = "1=1";
        if (siteMappingId.HasValue)   where += " AND site_mapping_id = @siteMappingId";
        if (xmlSourceId.HasValue)     where += " AND xml_source_id = @xmlSourceId";
        if (targetCompanyId.HasValue) where += " AND target_company_id = @targetCompanyId";
        if (status.HasValue)          where += " AND status = @status";

        using var conn = factory.CreateConnection();
        var multi = await conn.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM transfer_logs WHERE {where};
            SELECT * FROM transfer_logs WHERE {where} ORDER BY {orderColumn} {direction} LIMIT @pageSize OFFSET @offset
            """, new { siteMappingId, xmlSourceId, targetCompanyId, status, pageSize, offset });

        var total = await multi.ReadSingleAsync<long>();
        var items = (await multi.ReadAsync<TransferLog>()).ToList();

        return (items, total);
    }

    public async Task<IEnumerable<TransferLog>> GetRecentFailuresAsync(int take = 50, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
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
        using var conn = factory.CreateConnection();
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
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync(sql, log) > 0;
    }

    public async Task<bool> UpdateStatusAsync(long id, byte status, long? durationMs, string? errorMessage = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE transfer_logs SET status = @status, end_date = NOW(), duration_ms = @durationMs, error_message = @errorMessage WHERE id = @id",
            new { id, status, durationMs, errorMessage }) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM transfer_logs WHERE id = @id", new { id }) > 0;
    }
}

using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Data.Interfaces;

/// <summary>
/// Transfer Log CRUD + search (Dapper)
/// Job execution history ve diagnostics
/// </summary>
public interface ITransferLogRepository
{
    Task<TransferLog?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IEnumerable<TransferLog>> GetByXmlSourceIdAsync(long xmlSourceId, int take = 50, CancellationToken ct = default);
    Task<IEnumerable<TransferLog>> GetBySiteMappingIdAsync(long siteMappingId, int take = 50, CancellationToken ct = default);
    Task<IEnumerable<TransferLog>> GetByJobTypeAsync(string jobType, int take = 100, CancellationToken ct = default);
    Task<IEnumerable<TransferLog>> GetRecentFailuresAsync(int take = 50, CancellationToken ct = default);
    Task<long> CreateAsync(TransferLog log, CancellationToken ct = default);
    Task<bool> UpdateAsync(TransferLog log, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(long id, byte status, long? durationMs, string? errorMessage = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

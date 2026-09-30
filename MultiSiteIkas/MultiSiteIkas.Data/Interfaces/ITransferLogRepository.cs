using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Data.Interfaces;

public interface ITransferLogRepository
{
    Task<TransferLog?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<(IEnumerable<TransferLog> Items, long TotalCount)> GetPagedAsync(int page, int pageSize, long? siteMappingId = null, long? xmlSourceId = null, long? targetCompanyId = null, byte? status = null, string? sortField = null, string? sortDir = null, CancellationToken ct = default);
    Task<IEnumerable<TransferLog>> GetRecentFailuresAsync(int take = 50, CancellationToken ct = default);
    Task<long> CreateAsync(TransferLog log, CancellationToken ct = default);
    Task<bool> UpdateAsync(TransferLog log, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(long id, byte status, long? durationMs, string? errorMessage = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

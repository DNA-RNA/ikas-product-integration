using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Data.Interfaces;

/// <summary>
/// Product Transfer CRUD + status tracking (Dapper)
/// </summary>
public interface IProductTransferRepository
{
    Task<ProductTransfer?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ProductTransfer?> GetBySourceAndTargetAsync(long sourceProductId, long targetCompanyId, CancellationToken ct = default);
    Task<IEnumerable<ProductTransfer>> GetBySourceProductIdAsync(long sourceProductId, CancellationToken ct = default);
    Task<IEnumerable<ProductTransfer>> GetByTargetCompanyIdAsync(long targetCompanyId, CancellationToken ct = default);
    Task<IEnumerable<ProductTransfer>> GetBySiteMappingIdAsync(long siteMappingId, CancellationToken ct = default);
    Task<IEnumerable<ProductTransfer>> GetPendingAsync(CancellationToken ct = default);
    Task<IEnumerable<ProductTransfer>> GetFailedAsync(CancellationToken ct = default);
    Task<long> CreateAsync(ProductTransfer transfer, CancellationToken ct = default);
    Task<bool> UpdateAsync(ProductTransfer transfer, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(long id, byte status, string? errorMessage = null, CancellationToken ct = default);
    Task<bool> IncrementRetryCountAsync(long id, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

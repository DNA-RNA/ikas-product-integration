using MultiSiteIkas.Core.Transfer;

namespace MultiSiteIkas.Core.Interfaces;

public interface ITransferService
{
    Task<TransferResult> RunTransferAsync(long siteMappingId, CancellationToken ct = default);
}

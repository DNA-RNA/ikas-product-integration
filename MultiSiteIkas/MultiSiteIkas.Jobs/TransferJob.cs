using Hangfire;
using Microsoft.Extensions.Logging;
using MultiSiteIkas.Core.Interfaces;

namespace MultiSiteIkas.Jobs;

public class TransferJob(ITransferService transfer, ILogger<TransferJob> logger)
{
    [Queue("transfer")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 1800])]
    public async Task ExecuteAsync(long siteMappingId, CancellationToken ct = default)
    {
        logger.LogInformation("[Transfer] Mapping {Id} başlatılıyor", siteMappingId);

        var result = await transfer.RunTransferAsync(siteMappingId, ct);

        logger.LogInformation("[Transfer] Mapping {Id} tamamlandı — {Success}/{Total} başarılı, {Failed} hatalı, {Duration}",
            siteMappingId, result.SuccessCount, result.TotalProductsFetched, result.FailedCount, result.Duration);
    }
}

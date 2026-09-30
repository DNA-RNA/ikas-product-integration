using Hangfire;
using Microsoft.Extensions.Logging;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Jobs;

/// <summary>
/// Tüm aktif kaynaklar / mapping'ler için bireysel job'ları sıraya alır.
/// Recurring schedule bu job'u tetikler; iş akışı buradan dağılır.
/// </summary>
public class OrchestratorJob(
    IXmlSourceRepository xmlSources,
    ISiteMappingRepository siteMappings,
    IBackgroundJobClient backgroundJob,
    ILogger<OrchestratorJob> logger)
{
    [Queue("orchestrator")]
    public async Task RunXmlPullAllAsync(CancellationToken ct = default)
    {
        var sources = (await xmlSources.GetActiveAsync(ct)).ToList();
        logger.LogInformation("[Orchestrator] {Count} aktif XML source için pull job'ları sıraya alınıyor", sources.Count);

        foreach (var source in sources)
        {
            backgroundJob.Enqueue<XmlPullJob>(j => j.ExecuteAsync(source.Id, CancellationToken.None));
            logger.LogInformation("[Orchestrator] XmlPull sıraya alındı — source {Id} ({Name})", source.Id, source.Name);
        }
    }

    [Queue("orchestrator")]
    public async Task RunTransferAllAsync(CancellationToken ct = default)
    {
        var mappings = (await siteMappings.GetAllActiveAsync(ct)).ToList();
        logger.LogInformation("[Orchestrator] {Count} aktif mapping için transfer job'ları sıraya alınıyor", mappings.Count);

        foreach (var mapping in mappings)
        {
            backgroundJob.Enqueue<TransferJob>(j => j.ExecuteAsync(mapping.Id, CancellationToken.None));
            logger.LogInformation("[Orchestrator] Transfer sıraya alındı — mapping {Id}", mapping.Id);
        }
    }
}

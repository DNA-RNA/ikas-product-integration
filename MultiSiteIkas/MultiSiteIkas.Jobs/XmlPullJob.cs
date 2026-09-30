using Hangfire;
using Microsoft.Extensions.Logging;
using MultiSiteIkas.Core.Interfaces;

namespace MultiSiteIkas.Jobs;

public class XmlPullJob(IXmlPullService xmlPull, ILogger<XmlPullJob> logger)
{
    [Queue("xml-pull")]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900])]
    public async Task ExecuteAsync(long xmlSourceId, CancellationToken ct = default)
    {
        logger.LogInformation("[XmlPull] Source {Id} başlatılıyor", xmlSourceId);

        var result = await xmlPull.PullAsync(xmlSourceId, ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"XmlPull başarısız (source={xmlSourceId}): {result.Error}");

        logger.LogInformation("[XmlPull] Source {Id} tamamlandı — {Upserted} ürün, {Duration}",
            xmlSourceId, result.Upserted, result.Duration);
    }
}

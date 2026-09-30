namespace MultiSiteIkas.API.Models.Responses;

public sealed class DashboardStatsDto
{
    public int TotalCompanies { get; init; }
    public int ActiveCompanies { get; init; }
    public int ActiveXmlSources { get; init; }
    public int ActiveSiteMappings { get; init; }
    public long TotalProducts { get; init; }
    public long TotalTransfers { get; init; }
    public long SuccessfulTransfers { get; init; }
    public long FailedTransfers { get; init; }
    public DateTime? LastTransferDate { get; init; }
    public long TodaySuccessCount { get; init; }
    public long TodayFailedCount { get; init; }
}

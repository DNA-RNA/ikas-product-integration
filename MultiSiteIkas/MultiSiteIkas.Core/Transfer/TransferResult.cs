namespace MultiSiteIkas.Core.Transfer;

public sealed class TransferResult
{
    public long SiteMappingId { get; init; }
    public string StoreCode { get; init; } = null!;
    public int TotalProductsFetched { get; init; }
    public int FilteredCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public int SkippedCount { get; init; }
    public TimeSpan Duration { get; init; }
    public bool IsSuccess => FailedCount == 0;
}

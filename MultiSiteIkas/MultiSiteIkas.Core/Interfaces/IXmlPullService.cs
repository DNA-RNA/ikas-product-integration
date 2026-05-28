namespace MultiSiteIkas.Core.Interfaces;

public interface IXmlPullService
{
    Task<XmlPullResult> PullAsync(long xmlSourceId, CancellationToken ct = default);
}

public sealed class XmlPullResult
{
    public long XmlSourceId { get; init; }
    public int ProductsFound { get; init; }
    public int Upserted { get; init; }
    public TimeSpan Duration { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => Error is null;
}

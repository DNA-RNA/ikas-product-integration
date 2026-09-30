namespace MultiSiteIkas.API.Models.Responses;

public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrev => Page > 1;

    public static PagedResponse<T> From(
        (IEnumerable<T> Items, long TotalCount) result, int page, int pageSize) => new()
    {
        Items      = result.Items.ToList(),
        Page       = page,
        PageSize   = pageSize,
        TotalCount = result.TotalCount
    };
}
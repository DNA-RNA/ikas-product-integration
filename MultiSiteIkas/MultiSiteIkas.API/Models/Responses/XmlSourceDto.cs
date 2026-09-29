using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.API.Models.Responses;

public sealed class XmlSourceDto
{
    public long Id { get; init; }
    public string Name { get; init; } = null!;
    public long SourceCompanyId { get; init; }
    public string XmlUrl { get; init; } = null!;
    public bool IsActive { get; init; }
    public int SyncFrequencyHours { get; init; }
    public DateTime? LastSyncDate { get; init; }
    public DateTime? NextSyncDate { get; init; }
    public string? LastSyncStatus { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }

    public static XmlSourceDto From(XmlSource x) => new()
    {
        Id                 = x.Id,
        Name               = x.Name,
        SourceCompanyId    = x.SourceCompanyId,
        XmlUrl             = x.XmlUrl,
        IsActive           = x.IsActive,
        SyncFrequencyHours = x.SyncFrequencyHours,
        LastSyncDate       = x.LastSyncDate,
        NextSyncDate       = x.NextSyncDate,
        LastSyncStatus     = x.LastSyncStatus,
        CreatedDate        = x.CreatedDate,
        UpdatedDate        = x.UpdatedDate
    };
}
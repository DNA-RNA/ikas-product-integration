using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.API.Models.Responses;

public sealed class TransferLogDto
{
    public long Id { get; init; }
    public long XmlSourceId { get; init; }
    public long SiteMappingId { get; init; }
    public long TargetCompanyId { get; init; }
    public string JobType { get; init; } = null!;
    public string? HangfireJobId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public long? DurationMs { get; init; }
    public int TotalProductsProcessed { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public int SkippedCount { get; init; }
    public string StatusLabel { get; init; } = null!;  // Pending / Success / Failed
    public string? ErrorMessage { get; init; }

    public static TransferLogDto From(TransferLog l) => new()
    {
        Id                     = l.Id,
        XmlSourceId            = l.XmlSourceId,
        SiteMappingId          = l.SiteMappingId,
        TargetCompanyId        = l.TargetCompanyId,
        JobType                = l.JobType,
        HangfireJobId          = l.HangfireJobId,
        StartDate              = l.StartDate,
        EndDate                = l.EndDate,
        DurationMs             = l.DurationMs,
        TotalProductsProcessed = l.TotalProductsProcessed,
        SuccessCount           = l.SuccessCount,
        FailedCount            = l.FailedCount,
        SkippedCount           = l.SkippedCount,
        StatusLabel            = l.Status switch { 1 => "Success", 2 => "Failed", _ => "Pending" },
        ErrorMessage           = l.ErrorMessage
    };
}
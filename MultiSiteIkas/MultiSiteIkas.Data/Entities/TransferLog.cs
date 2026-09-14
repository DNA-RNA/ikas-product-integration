namespace MultiSiteIkas.Data.Entities;

/// <summary>
/// Job çalışma geçmişi ve özeti
/// Detaylı başarı/başarısızlık kaydı
/// </summary>
public class TransferLog
{
    public long Id { get; set; }
    public long XmlSourceId { get; set; }
    public long SiteMappingId { get; set; }
    public long TargetCompanyId { get; set; }
    public string JobType { get; set; } = null!; // 'XmlPull', 'Transfer', 'HealthCheck'
    public string? HangfireJobId { get; set; }

    // Execution Details
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public long? DurationMs { get; set; }

    // Results Summary
    public int TotalProductsProcessed { get; set; } = 0;
    public int SuccessCount { get; set; } = 0;
    public int FailedCount { get; set; } = 0;
    public int SkippedCount { get; set; } = 0;

    // Status & Error
    public byte Status { get; set; } = 0; // 0=Pending, 1=Success, 2=Failed
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }

    // Detailed Log
    public string? DetailJson { get; set; } // JSON: {failedSkus: [..], errors: [..]}

    // Record
    public DateTime CreatedDate { get; set; }
}

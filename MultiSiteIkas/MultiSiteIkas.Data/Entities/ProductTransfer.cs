namespace MultiSiteIkas.Data.Entities;

/// <summary>
/// Hangi ürünün hangi hedefe gittiği ve transfer durumu
/// </summary>
public class ProductTransfer
{
    public long Id { get; set; }
    public long SourceProductId { get; set; }
    public long TargetCompanyId { get; set; }
    public long SiteMappingId { get; set; }

    // İkas Tarafı
    public string? IkasProductId { get; set; }
    public string? IkasVariantId { get; set; }
    public string? TargetSku { get; set; }

    // Transfer Details
    public decimal? TransferredPrice { get; set; }
    public string? TransferredCategory { get; set; }
    public byte TransferStatus { get; set; } = 0; // 0=Pending, 1=Success, 2=Failed, 3=Skipped
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;

    // Dates
    public DateTime? FirstTransferDate { get; set; }
    public DateTime? LastTransferDate { get; set; }
    public DateTime CreatedDate { get; set; }
}

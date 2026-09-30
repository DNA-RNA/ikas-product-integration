using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.API.Models.Responses;

public sealed class ProductTransferDto
{
    public long Id { get; init; }
    public long SourceProductId { get; init; }
    public long TargetCompanyId { get; init; }
    public long SiteMappingId { get; init; }
    public string? IkasProductId { get; init; }
    public string? TargetSku { get; init; }
    public decimal? TransferredPrice { get; init; }
    public string? TransferredCategory { get; init; }
    public byte TransferStatus { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
    public DateTime? FirstTransferDate { get; init; }
    public DateTime? LastTransferDate { get; init; }

    public static ProductTransferDto From(ProductTransfer t) => new()
    {
        Id                  = t.Id,
        SourceProductId     = t.SourceProductId,
        TargetCompanyId     = t.TargetCompanyId,
        SiteMappingId       = t.SiteMappingId,
        IkasProductId       = t.IkasProductId,
        TargetSku           = t.TargetSku,
        TransferredPrice    = t.TransferredPrice,
        TransferredCategory = t.TransferredCategory,
        TransferStatus      = t.TransferStatus,
        ErrorMessage        = t.ErrorMessage,
        RetryCount          = t.RetryCount,
        FirstTransferDate   = t.FirstTransferDate,
        LastTransferDate    = t.LastTransferDate
    };
}

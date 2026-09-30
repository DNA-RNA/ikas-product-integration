using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.API.Models.Responses;

public sealed class SiteMappingDto
{
    public long Id { get; init; }
    public long XmlSourceId { get; init; }
    public long TargetCompanyId { get; init; }
    public decimal PriceMarginPercentage { get; init; }
    public decimal AdditionalPrice { get; init; }
    public string? CurrencyOverride { get; init; }
    public string? CategoryFilters { get; init; }
    public string? CategoryMappings { get; init; }
    public string? BrandMappings { get; init; }
    public bool DeactivateZeroStock { get; init; }
    public bool SendImages { get; init; }
    public string? SyncCron { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }

    public static SiteMappingDto From(SiteMapping s) => new()
    {
        Id                     = s.Id,
        XmlSourceId            = s.XmlSourceId,
        TargetCompanyId        = s.TargetCompanyId,
        PriceMarginPercentage  = s.PriceMarginPercentage,
        AdditionalPrice        = s.AdditionalPrice,
        CurrencyOverride       = s.CurrencyOverride,
        CategoryFilters        = s.CategoryFilters,
        CategoryMappings       = s.CategoryMappings,
        BrandMappings          = s.BrandMappings,
        DeactivateZeroStock    = s.DeactivateZeroStock,
        SendImages             = s.SendImages,
        SyncCron               = s.SyncCron,
        IsActive               = s.IsActive,
        CreatedDate            = s.CreatedDate,
        UpdatedDate            = s.UpdatedDate
    };
}
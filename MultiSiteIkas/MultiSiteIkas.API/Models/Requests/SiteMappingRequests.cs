using System.ComponentModel.DataAnnotations;

namespace MultiSiteIkas.API.Models.Requests;

public sealed class CreateSiteMappingRequest
{
    [Required]
    public long XmlSourceId { get; init; }

    [Required]
    public long TargetCompanyId { get; init; }

    [Range(0, 1000)]
    public decimal PriceMarginPercentage { get; init; } = 0;

    [Range(0, double.MaxValue)]
    public decimal AdditionalPrice { get; init; } = 0;

    [MaxLength(10)]
    public string? CurrencyOverride { get; init; }

    public string? CategoryFilters { get; init; }   // JSON: ["Reçine", "Boncuk"]
    public string? CategoryMappings { get; init; }  // JSON: {"Hobi > Reçine": "Resin"}
    public string? BrandMappings { get; init; }     // JSON: {"BrandA": "Brand A"}

    public bool DeactivateZeroStock { get; init; } = true;
    public bool SendImages { get; init; } = true;

    [MaxLength(100)]
    public string? SyncCron { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class UpdateSiteMappingRequest
{
    [Range(0, 1000)]
    public decimal PriceMarginPercentage { get; init; }

    [Range(0, double.MaxValue)]
    public decimal AdditionalPrice { get; init; }

    [MaxLength(10)]
    public string? CurrencyOverride { get; init; }

    public string? CategoryFilters { get; init; }
    public string? CategoryMappings { get; init; }
    public string? BrandMappings { get; init; }

    public bool DeactivateZeroStock { get; init; }
    public bool SendImages { get; init; }

    [MaxLength(100)]
    public string? SyncCron { get; init; }

    public bool IsActive { get; init; }
}
namespace MultiSiteIkas.Data.Entities;

/// <summary>
/// XML Source → Target Company Mapping
/// Filtering + pricing rules tutar (her target company özelinde)
/// İkas API credentials companies tablosunda saklanır
/// </summary>
public class SiteMapping
{
    public long Id { get; set; }
    public long XmlSourceId { get; set; }
    public long TargetCompanyId { get; set; }

    // Pricing Rules
    public decimal PriceMarginPercentage { get; set; } = 0;
    public decimal AdditionalPrice { get; set; } = 0;
    public string? CurrencyOverride { get; set; }

    // Filters & Mappings (JSON)
    public string? CategoryFilters { get; set; } // JSON: ["Reçine", "Boncuk > *"]
    public string? CategoryMappings { get; set; } // JSON: {"Hobi > Reçine": "Resin"}
    public string? BrandMappings { get; set; } // JSON: {"EpoxyPro": "EpoxyPro EN"}

    // Behavior
    public bool DeactivateZeroStock { get; set; } = true;
    public bool SendImages { get; set; } = true;

    // Schedule (opsiyonel; null = global default)
    public string? SyncCron { get; set; } // '30 2 * * *'

    // Status
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

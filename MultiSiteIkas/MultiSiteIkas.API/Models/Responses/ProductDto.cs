using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.API.Models.Responses;

public sealed class ProductDto
{
    public long Id { get; init; }
    public long XmlSourceId { get; init; }
    public string? ExternalId { get; init; }
    public string Sku { get; init; } = null!;
    public string? Barcode { get; init; }
    public string Name { get; init; } = null!;
    public string CategoryPath { get; init; } = null!;
    public string? Brand { get; init; }
    public decimal OriginalPrice { get; init; }
    public decimal SalePrice { get; init; }
    public string Currency { get; init; } = null!;
    public int StockQuantity { get; init; }
    public bool IsActive { get; init; }
    public DateTime LastSeenDate { get; init; }
    public DateTime? UpdatedDate { get; init; }

    public static ProductDto From(Product p) => new()
    {
        Id            = p.Id,
        XmlSourceId   = p.XmlSourceId,
        ExternalId    = p.ExternalId,
        Sku           = p.Sku,
        Barcode       = p.Barcode,
        Name          = p.Name,
        CategoryPath  = p.CategoryPath,
        Brand         = p.Brand,
        OriginalPrice = p.OriginalPrice,
        SalePrice     = p.SalePrice,
        Currency      = p.Currency,
        StockQuantity = p.StockQuantity,
        IsActive      = p.IsActive,
        LastSeenDate  = p.LastSeenDate,
        UpdatedDate   = p.UpdatedDate
    };
}

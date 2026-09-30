namespace MultiSiteIkas.Core.Xml;

public sealed class ParsedProduct
{
    public string? ExternalId { get; set; }
    public required string Sku { get; set; }
    public string? Barcode { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string CategoryPath { get; set; }
    public string? Brand { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string Currency { get; set; } = "TRY";
    public int StockQuantity { get; set; }
    public decimal? Weight { get; set; }
    public List<string> Images { get; set; } = new();
    public Dictionary<string, string> Attributes { get; set; } = new();
}

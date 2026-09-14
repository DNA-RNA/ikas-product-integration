namespace MultiSiteIkas.Data.Entities;

/// <summary>
/// Master XML'den parse edilmiş ürünler
/// SKU bazlı unique constraint: (xml_source_id, sku)
/// is_deleted: soft delete (master'da silindiğinde hedefte pasif yapılacak)
/// </summary>
public class Product
{
    public long Id { get; set; }
    public long CompanyId { get; set; } // master mağaza (hobizubi.com)
    public long XmlSourceId { get; set; }
    public string? ExternalId { get; set; } // master mağaza internal id

    // Product Details
    public string Sku { get; set; } = null!;
    public string? Barcode { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string CategoryPath { get; set; } = null!;
    public string? Brand { get; set; }

    // Pricing
    public decimal OriginalPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string Currency { get; set; } = "TRY";

    // Inventory
    public int StockQuantity { get; set; } = 0;
    public decimal? Weight { get; set; }

    // Extra
    public string? ImagesJson { get; set; } // JSON array of URLs
    public string? AttributesJson { get; set; } // JSON object

    // Status & Lifecycle
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false; // soft delete (master'da silinirse)
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime LastSeenDate { get; set; } // son XML'de görüldüğü tarih
}

namespace MultiSiteIkas.Core.Ikas;

public sealed record IkasCredentials(string ApiKey, string ApiSecret, string StoreCode);

// ── Mapper çıktısı — TransferService/IkasFieldMapper bu modeli kullanır ──────

public sealed class IkasProductInput
{
    public string? Id { get; set; }         // mevcut ürün ID'si (update için)

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Type { get; set; } = "PHYSICAL";
    public decimal? Weight { get; set; }

    public string? BrandName { get; set; }
    public string? CategoryName { get; set; }

    public IkasVariantInput[] Variants { get; set; } = [];

    public string? MetaSlug { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
}

public sealed class IkasVariantInput
{
    public string? VariantId { get; set; }   // mevcut variant ID'si (update için)
    public string? Sku { get; set; }
    public string[]? BarcodeList { get; set; }
    public decimal SellPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public IkasImageInput[] Images { get; set; } = [];
    public float? Weight { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new(); // Renk, Beden vs.
}

public sealed class IkasImageInput
{
    public int Order { get; set; }
    public bool IsMain { get; set; }
    public string FileName { get; set; } = null!;
}

// ── Yanıt modelleri ────────────────────────────────────────────────────────────

public sealed class IkasProduct
{
    public string Id { get; set; } = null!;
    public string? Name { get; set; }
    public List<IkasVariant> Variants { get; set; } = new();
}

public sealed class IkasVariant
{
    public string Id { get; set; } = null!;
    public string? Sku { get; set; }
}

public sealed class IkasCategory
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? ParentId { get; set; }
}

// ── GraphQL altyapı ────────────────────────────────────────────────────────────

internal sealed class GraphQLRequest
{
    public required string Query { get; init; }
    public object? Variables { get; init; }
    public string? OperationName { get; init; }
}

internal sealed class GraphQLResponse<T>
{
    public T? Data { get; init; }
    public List<GraphQLError>? Errors { get; init; }
}

internal sealed class GraphQLError
{
    public required string Message { get; init; }
    public Dictionary<string, object>? Extensions { get; init; }
}

internal sealed class SaveProductData
{
    public IkasProduct CreateProduct { get; set; } = null!;
}

internal sealed class UpdateProductData
{
    public IkasProduct UpdateProduct { get; set; } = null!;
}

internal sealed class ListProductData
{
    public IkasProductList ListProduct { get; set; } = null!;
}

internal sealed class IkasProductList
{
    public List<IkasProduct> Data { get; set; } = new();
    public int Count { get; set; }
}

internal sealed class ListCategoryData
{
    public List<IkasCategory> ListCategory { get; set; } = new();
}

// ── v2 API İç input tipleri (IkasApiService tarafından kullanılır) ─────────────

internal sealed class CreateProductInputV2
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = "PHYSICAL";
    public string? Description { get; set; }
    public float? Weight { get; set; }
    public BrandInputV2? Brand { get; set; }
    public CategoryInputV2[]? Categories { get; set; }
    public CreateVariantInputV2[]? Variants { get; set; }
    public CreateMetaDataInputV2? MetaData { get; set; }
}

internal sealed class UpdateProductInputV2
{
    public string Id { get; set; } = null!;
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public float? Weight { get; set; }
    public BrandInputV2? Brand { get; set; }
    public CategoryInputV2[]? Categories { get; set; }
    public UpdateVariantInputV2[]? Variants { get; set; }
    public UpdateMetaDataInputV2? MetaData { get; set; }
}

internal sealed class BrandInputV2 { public string Name { get; set; } = null!; }
internal sealed class CategoryInputV2 { public string Name { get; set; } = null!; }

internal sealed class CreateVariantInputV2
{
    public string? Sku { get; set; }
    public bool IsActive { get; set; } = true;
    public string[]? BarcodeList { get; set; }
    public PriceInputV2[]? Prices { get; set; }
    public VariantImageInputV2[]? Images { get; set; }
    public float? Weight { get; set; }
    public bool? SellIfOutOfStock { get; set; }
    public VariantValueInput[]? VariantValues { get; set; }
}

internal sealed class VariantValueInput
{
    public string VariantTypeName { get; set; } = null!;
    public string VariantValueName { get; set; } = null!;
}

internal sealed class UpdateVariantInputV2
{
    public string Id { get; set; } = null!;
    public string? Sku { get; set; }
    public bool? IsActive { get; set; }
    public PriceInputV2[]? Prices { get; set; }
}

internal sealed class PriceInputV2
{
    public decimal SellPrice { get; set; }
    public decimal? DiscountPrice { get; set; }
}

internal sealed class VariantImageInputV2
{
    public string ImageUrl { get; set; } = null!;
    public int Order { get; set; }
    public bool IsMain { get; set; }
}

internal sealed class CreateMetaDataInputV2
{
    public string Slug { get; set; } = null!;
    public string? PageTitle { get; set; }
    public string? Description { get; set; }
}

internal sealed class UpdateMetaDataInputV2
{
    public string? PageTitle { get; set; }
    public string? Description { get; set; }
}

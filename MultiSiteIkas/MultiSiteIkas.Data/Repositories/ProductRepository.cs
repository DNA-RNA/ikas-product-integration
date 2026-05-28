using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _factory;

    public ProductRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<Product?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Product>(
            "SELECT * FROM products WHERE id = @id", new { id });
    }

    public async Task<Product?> GetBySkuAsync(string sku, long xmlSourceId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Product>(
            "SELECT * FROM products WHERE sku = @sku AND xml_source_id = @xmlSourceId",
            new { sku, xmlSourceId });
    }

    public async Task<IEnumerable<Product>> GetByXmlSourceIdAsync(long xmlSourceId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Product>(
            "SELECT * FROM products WHERE xml_source_id = @xmlSourceId AND is_deleted = FALSE",
            new { xmlSourceId });
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string categoryPath, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Product>(
            "SELECT * FROM products WHERE category_path = @categoryPath AND is_deleted = FALSE",
            new { categoryPath });
    }

    public async Task<IEnumerable<Product>> GetActiveAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Product>(
            "SELECT * FROM products WHERE is_active = TRUE AND is_deleted = FALSE");
    }

    public async Task<IEnumerable<Product>> GetInactiveAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Product>(
            "SELECT * FROM products WHERE is_active = FALSE OR is_deleted = TRUE");
    }

    public async Task<long> UpsertAsync(Product product, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO products (
                company_id, xml_source_id, external_id, sku, barcode,
                name, description, category_path, brand,
                original_price, sale_price, discount_price, currency,
                stock_quantity, weight, images_json, attributes_json,
                is_active, is_deleted, created_date, last_seen_date
            ) VALUES (
                @CompanyId, @XmlSourceId, @ExternalId, @Sku, @Barcode,
                @Name, @Description, @CategoryPath, @Brand,
                @OriginalPrice, @SalePrice, @DiscountPrice, @Currency,
                @StockQuantity, @Weight, @ImagesJson, @AttributesJson,
                @IsActive, @IsDeleted, NOW(), NOW()
            )
            ON CONFLICT (xml_source_id, sku) DO UPDATE SET
                external_id     = EXCLUDED.external_id,
                barcode         = EXCLUDED.barcode,
                name            = EXCLUDED.name,
                description     = EXCLUDED.description,
                category_path   = EXCLUDED.category_path,
                brand           = EXCLUDED.brand,
                original_price  = EXCLUDED.original_price,
                sale_price      = EXCLUDED.sale_price,
                discount_price  = EXCLUDED.discount_price,
                currency        = EXCLUDED.currency,
                stock_quantity  = EXCLUDED.stock_quantity,
                weight          = EXCLUDED.weight,
                images_json     = EXCLUDED.images_json,
                attributes_json = EXCLUDED.attributes_json,
                is_active       = EXCLUDED.is_active,
                is_deleted      = EXCLUDED.is_deleted,
                updated_date    = NOW(),
                last_seen_date  = NOW()
            RETURNING id
            """;

        using var conn = _factory.CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, product);
    }

    public async Task<int> BulkUpsertAsync(IEnumerable<Product> products, CancellationToken ct = default)
    {
        var list = products as IList<Product> ?? products.ToList();
        var total = 0;

        foreach (var product in list)
        {
            await UpsertAsync(product, ct);
            total++;
        }

        return total;
    }

    public async Task<bool> UpdateAsync(Product product, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE products SET
                external_id     = @ExternalId,
                barcode         = @Barcode,
                name            = @Name,
                description     = @Description,
                category_path   = @CategoryPath,
                brand           = @Brand,
                original_price  = @OriginalPrice,
                sale_price      = @SalePrice,
                discount_price  = @DiscountPrice,
                currency        = @Currency,
                stock_quantity  = @StockQuantity,
                weight          = @Weight,
                images_json     = @ImagesJson,
                attributes_json = @AttributesJson,
                is_active       = @IsActive,
                is_deleted      = @IsDeleted,
                updated_date    = NOW()
            WHERE id = @Id
            """;

        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(sql, product) > 0;
    }

    public async Task<bool> UpdateStockAsync(long id, int newStock, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE products SET stock_quantity = @newStock, updated_date = NOW() WHERE id = @id",
            new { id, newStock }) > 0;
    }

    public async Task<bool> MarkDeletedAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE products SET is_deleted = TRUE, is_active = FALSE, updated_date = NOW() WHERE id = @id",
            new { id }) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "DELETE FROM products WHERE id = @id", new { id }) > 0;
    }
}

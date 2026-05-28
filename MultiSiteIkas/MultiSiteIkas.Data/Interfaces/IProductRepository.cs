using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Data.Interfaces;

/// <summary>
/// Product CRUD + upsert + bulk operations (Dapper)
/// SKU bazlı unique, idempotent upsert
/// </summary>
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Product?> GetBySkuAsync(string sku, long xmlSourceId, CancellationToken ct = default);
    Task<IEnumerable<Product>> GetByXmlSourceIdAsync(long xmlSourceId, CancellationToken ct = default);
    Task<IEnumerable<Product>> GetByCategoryAsync(string categoryPath, CancellationToken ct = default);
    Task<IEnumerable<Product>> GetActiveAsync(CancellationToken ct = default);
    
    /// <summary>
    /// SKU bazlı upsert: var ise update, yoksa insert
    /// </summary>
    Task<long> UpsertAsync(Product product, CancellationToken ct = default);
    
    /// <summary>
    /// Bulk upsert (performance critical)
    /// </summary>
    Task<int> BulkUpsertAsync(IEnumerable<Product> products, CancellationToken ct = default);
    
    Task<bool> UpdateAsync(Product product, CancellationToken ct = default);
    Task<bool> UpdateStockAsync(long id, int newStock, CancellationToken ct = default);
    Task<bool> MarkDeletedAsync(long id, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    
    /// <summary>
    /// Inactive products (stok = 0 veya is_deleted = 1)
    /// </summary>
    Task<IEnumerable<Product>> GetInactiveAsync(CancellationToken ct = default);
}

using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class ProductTransferRepository : IProductTransferRepository
{
    private readonly IDbConnectionFactory _factory;

    public ProductTransferRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<ProductTransfer?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ProductTransfer>(
            "SELECT * FROM product_transfers WHERE id = @id", new { id });
    }

    public async Task<ProductTransfer?> GetBySourceAndTargetAsync(long sourceProductId, long targetCompanyId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ProductTransfer>(
            "SELECT * FROM product_transfers WHERE source_product_id = @sourceProductId AND target_company_id = @targetCompanyId",
            new { sourceProductId, targetCompanyId });
    }

    public async Task<IEnumerable<ProductTransfer>> GetBySourceProductIdAsync(long sourceProductId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<ProductTransfer>(
            "SELECT * FROM product_transfers WHERE source_product_id = @sourceProductId",
            new { sourceProductId });
    }

    public async Task<IEnumerable<ProductTransfer>> GetByTargetCompanyIdAsync(long targetCompanyId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<ProductTransfer>(
            "SELECT * FROM product_transfers WHERE target_company_id = @targetCompanyId",
            new { targetCompanyId });
    }

    public async Task<IEnumerable<ProductTransfer>> GetBySiteMappingIdAsync(long siteMappingId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<ProductTransfer>(
            "SELECT * FROM product_transfers WHERE site_mapping_id = @siteMappingId",
            new { siteMappingId });
    }

    public async Task<IEnumerable<ProductTransfer>> GetPendingAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<ProductTransfer>(
            "SELECT * FROM product_transfers WHERE transfer_status = 0");
    }

    public async Task<IEnumerable<ProductTransfer>> GetFailedAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<ProductTransfer>(
            "SELECT * FROM product_transfers WHERE transfer_status = 2");
    }

    public async Task<long> CreateAsync(ProductTransfer transfer, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO product_transfers
                (source_product_id, target_company_id, site_mapping_id,
                 ikas_product_id, ikas_variant_id, target_sku,
                 transferred_price, transferred_category,
                 transfer_status, error_message, retry_count,
                 first_transfer_date, last_transfer_date, created_date)
            VALUES
                (@SourceProductId, @TargetCompanyId, @SiteMappingId,
                 @IkasProductId, @IkasVariantId, @TargetSku,
                 @TransferredPrice, @TransferredCategory,
                 @TransferStatus, @ErrorMessage, @RetryCount,
                 @FirstTransferDate, @LastTransferDate, NOW())
            RETURNING id
            """;
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, transfer);
    }

    public async Task<bool> UpdateAsync(ProductTransfer transfer, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE product_transfers SET
                ikas_product_id      = @IkasProductId,
                ikas_variant_id      = @IkasVariantId,
                target_sku           = @TargetSku,
                transferred_price    = @TransferredPrice,
                transferred_category = @TransferredCategory,
                transfer_status      = @TransferStatus,
                error_message        = @ErrorMessage,
                retry_count          = @RetryCount,
                last_transfer_date   = @LastTransferDate
            WHERE id = @Id
            """;
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(sql, transfer) > 0;
    }

    public async Task<bool> UpdateStatusAsync(long id, byte status, string? errorMessage = null, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE product_transfers SET transfer_status = @status, error_message = @errorMessage, last_transfer_date = NOW() WHERE id = @id",
            new { id, status, errorMessage }) > 0;
    }

    public async Task<bool> IncrementRetryCountAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "UPDATE product_transfers SET retry_count = retry_count + 1 WHERE id = @id",
            new { id }) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "DELETE FROM product_transfers WHERE id = @id", new { id }) > 0;
    }
}

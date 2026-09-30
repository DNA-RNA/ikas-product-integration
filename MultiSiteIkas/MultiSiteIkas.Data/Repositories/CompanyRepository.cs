using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class CompanyRepository(IDbConnectionFactory factory) : ICompanyRepository
{
    public async Task<Company?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Company>(
            "SELECT * FROM companies WHERE id = @id", new { id });
    }

    public async Task<Company?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Company>(
            "SELECT * FROM companies WHERE name = @name", new { name });
    }

    public async Task<(IEnumerable<Company> Items, long TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search = null, bool? isActive = null, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 10;
        var offset = (page - 1) * pageSize;

        var where = "1=1";
        if (!string.IsNullOrWhiteSpace(search))
            where += " AND (name ILIKE @searchLike OR email ILIKE @searchLike)";
        if (isActive.HasValue)
            where += " AND is_active = @isActive";

        var searchLike = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

        using var conn = factory.CreateConnection();
        var multi = await conn.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM companies WHERE {where};
            SELECT * FROM companies WHERE {where} ORDER BY created_date DESC LIMIT @pageSize OFFSET @offset
            """, new { searchLike, isActive, pageSize, offset });

        var total = await multi.ReadSingleAsync<long>();
        var items = (await multi.ReadAsync<Company>()).ToList();

        return (items, total);
    }

    public async Task<IEnumerable<Company>> GetActiveAsync(CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QueryAsync<Company>("SELECT * FROM companies WHERE is_active = TRUE");
    }

    public async Task<long> CreateAsync(Company company, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO companies (name, email, website_url, ikas_api_key, ikas_api_secret, language_code, is_active, created_date)
            VALUES (@Name, @Email, @WebsiteUrl, @IkasApiKey, @IkasApiSecret, @LanguageCode, @IsActive, NOW())
            RETURNING id
            """;
        using var conn = factory.CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, company);
    }

    public async Task<bool> UpdateAsync(Company company, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE companies SET
                name             = @Name,
                email            = @Email,
                website_url      = @WebsiteUrl,
                ikas_api_key     = @IkasApiKey,
                ikas_api_secret  = @IkasApiSecret,
                language_code    = @LanguageCode,
                is_active        = @IsActive,
                updated_date     = NOW()
            WHERE id = @Id
            """;
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync(sql, company) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM companies WHERE id = @id", new { id }) > 0;
    }
}

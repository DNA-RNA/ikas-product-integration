using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly IDbConnectionFactory _factory;

    public CompanyRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<Company?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Company>(
            "SELECT * FROM companies WHERE id = @id", new { id });
    }

    public async Task<Company?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Company>(
            "SELECT * FROM companies WHERE name = @name", new { name });
    }

    public async Task<IEnumerable<Company>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Company>("SELECT * FROM companies");
    }

    public async Task<IEnumerable<Company>> GetActiveAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Company>(
            "SELECT * FROM companies WHERE is_active = TRUE");
    }

    public async Task<long> CreateAsync(Company company, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO companies (name, email, website_url, ikas_api_key, ikas_api_secret, language_code, is_active, created_date)
            VALUES (@Name, @Email, @WebsiteUrl, @IkasApiKey, @IkasApiSecret, @LanguageCode, @IsActive, NOW())
            RETURNING id
            """;
        using var conn = _factory.CreateConnection();
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
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(sql, company) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        return await conn.ExecuteAsync(
            "DELETE FROM companies WHERE id = @id", new { id }) > 0;
    }
}

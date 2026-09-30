using Dapper;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.Data.Repositories;

public sealed class AppUserRepository(IDbConnectionFactory factory) : IAppUserRepository
{
    public async Task<AppUser?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AppUser>(
            "SELECT * FROM app_users WHERE id = @id AND is_active = TRUE", new { id });
    }

    public async Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AppUser>(
            "SELECT * FROM app_users WHERE username = @username AND is_active = TRUE",
            new { username });
    }

    public async Task<long> CreateAsync(AppUser user, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO app_users (username, password_hash, role, company_id, is_active, created_date)
            VALUES (@Username, @PasswordHash, @Role, @CompanyId, @IsActive, NOW())
            RETURNING id
            """;
        using var conn = factory.CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, user);
    }

    public async Task<bool> UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE app_users SET
                password_hash = @PasswordHash,
                role          = @Role,
                company_id    = @CompanyId,
                is_active     = @IsActive,
                updated_date  = NOW()
            WHERE id = @Id
            """;
        using var conn = factory.CreateConnection();
        return await conn.ExecuteAsync(sql, user) > 0;
    }
}

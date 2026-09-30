using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Data.Interfaces;

public interface IAppUserRepository
{
    Task<AppUser?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<long> CreateAsync(AppUser user, CancellationToken ct = default);
    Task<bool> UpdateAsync(AppUser user, CancellationToken ct = default);
}

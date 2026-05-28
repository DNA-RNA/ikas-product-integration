using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Data.Interfaces;

/// <summary>
/// Company CRUD + lookup operations (Dapper)
/// </summary>
public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Company?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IEnumerable<Company>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Company>> GetActiveAsync(CancellationToken ct = default);
    Task<long> CreateAsync(Company company, CancellationToken ct = default);
    Task<bool> UpdateAsync(Company company, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

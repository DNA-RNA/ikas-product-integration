using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Data.Interfaces;

/// <summary>
/// Site Mapping CRUD + filters/mappings (Dapper)
/// </summary>
public interface ISiteMappingRepository
{
    Task<SiteMapping?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IEnumerable<SiteMapping>> GetByXmlSourceIdAsync(long xmlSourceId, CancellationToken ct = default);
    Task<IEnumerable<SiteMapping>> GetByTargetCompanyIdAsync(long targetCompanyId, CancellationToken ct = default);
    Task<IEnumerable<SiteMapping>> GetAllActiveAsync(CancellationToken ct = default);
    Task<SiteMapping?> GetBySourceAndTargetAsync(long xmlSourceId, long targetCompanyId, CancellationToken ct = default);
    Task<long> CreateAsync(SiteMapping siteMapping, CancellationToken ct = default);
    Task<bool> UpdateAsync(SiteMapping siteMapping, CancellationToken ct = default);
    Task<bool> UpdateFiltersAsync(long id, string? categoryFilters, string? categoryMappings, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

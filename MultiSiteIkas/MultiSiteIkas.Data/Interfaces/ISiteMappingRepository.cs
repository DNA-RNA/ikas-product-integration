using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Data.Interfaces;

public interface ISiteMappingRepository
{
    Task<SiteMapping?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<(IEnumerable<SiteMapping> Items, long TotalCount)> GetPagedAsync(int page, int pageSize, long? targetCompanyId = null, bool? isActive = null, string? sortField = null, string? sortDir = null, CancellationToken ct = default);
    Task<IEnumerable<SiteMapping>> GetAllActiveAsync(CancellationToken ct = default);
    Task<IEnumerable<SiteMapping>> GetByXmlSourceIdAsync(long xmlSourceId, CancellationToken ct = default);
    Task<SiteMapping?> GetBySourceAndTargetAsync(long xmlSourceId, long targetCompanyId, CancellationToken ct = default);
    Task<long> CreateAsync(SiteMapping siteMapping, CancellationToken ct = default);
    Task<bool> UpdateAsync(SiteMapping siteMapping, CancellationToken ct = default);
    Task<bool> UpdateFiltersAsync(long id, string? categoryFilters, string? categoryMappings, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

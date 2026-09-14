using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Data.Interfaces;

/// <summary>
/// XML Source CRUD + schedule operations (Dapper)
/// </summary>
public interface IXmlSourceRepository
{
    Task<XmlSource?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IEnumerable<XmlSource>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<XmlSource>> GetActiveAsync(CancellationToken ct = default);
    Task<IEnumerable<XmlSource>> GetDueForSyncAsync(CancellationToken ct = default);
    Task<long> CreateAsync(XmlSource xmlSource, CancellationToken ct = default);
    Task<bool> UpdateAsync(XmlSource xmlSource, CancellationToken ct = default);
    Task<bool> UpdateSyncStatusAsync(long id, string status, DateTime nextSyncDate, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

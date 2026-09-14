using MultiSiteIkas.Core.Ikas;

namespace MultiSiteIkas.Core.Interfaces;

public interface IIkasApiService
{
    Task<IkasProduct?> FindBySkuAsync(IkasCredentials creds, string sku, CancellationToken ct = default);
    Task<IkasProduct> SaveProductAsync(IkasCredentials creds, IkasProductInput input, CancellationToken ct = default);
    Task<IReadOnlyList<IkasCategory>> ListCategoriesAsync(IkasCredentials creds, CancellationToken ct = default);
}

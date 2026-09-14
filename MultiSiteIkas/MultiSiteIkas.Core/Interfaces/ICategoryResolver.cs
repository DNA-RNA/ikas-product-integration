using MultiSiteIkas.Core.Ikas;

namespace MultiSiteIkas.Core.Interfaces;

public interface ICategoryResolver
{
    Task RefreshAsync(IkasCredentials creds, CancellationToken ct = default);
    string? ResolveId(string categoryName);
}

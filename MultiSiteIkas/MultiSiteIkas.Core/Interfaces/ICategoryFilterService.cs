namespace MultiSiteIkas.Core.Interfaces;

public interface ICategoryFilterService
{
    bool ShouldTransfer(string categoryPath, IReadOnlyList<string> filters);
}

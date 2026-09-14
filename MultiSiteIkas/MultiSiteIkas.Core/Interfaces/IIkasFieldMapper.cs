using MultiSiteIkas.Core.Ikas;
using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.Core.Interfaces;

public interface IIkasFieldMapper
{
    IkasProductInput Map(IReadOnlyList<Product> variants, SiteMapping mapping, string? categoryName);
}

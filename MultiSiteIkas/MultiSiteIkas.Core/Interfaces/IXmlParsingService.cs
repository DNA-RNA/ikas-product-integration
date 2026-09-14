using MultiSiteIkas.Core.Xml;

namespace MultiSiteIkas.Core.Interfaces;

public interface IXmlParsingService
{
    Task<List<ParsedProduct>> ParseAsync(Stream xmlStream, CancellationToken ct = default);
}

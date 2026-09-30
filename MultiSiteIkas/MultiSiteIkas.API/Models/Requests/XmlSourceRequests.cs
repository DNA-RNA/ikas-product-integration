using System.ComponentModel.DataAnnotations;

namespace MultiSiteIkas.API.Models.Requests;

public sealed class CreateXmlSourceRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = null!;

    [Required]
    public long SourceCompanyId { get; init; }

    [Required, Url, MaxLength(2000)]
    public string XmlUrl { get; init; } = null!;

    [Range(1, 168)]
    public int SyncFrequencyHours { get; init; } = 24;

    public bool IsActive { get; init; } = true;
}

public sealed class UpdateXmlSourceRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = null!;

    [Required, Url, MaxLength(2000)]
    public string XmlUrl { get; init; } = null!;

    [Range(1, 168)]
    public int SyncFrequencyHours { get; init; } = 24;

    public bool IsActive { get; init; }
}
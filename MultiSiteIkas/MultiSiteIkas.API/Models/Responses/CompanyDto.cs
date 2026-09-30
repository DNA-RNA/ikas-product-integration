using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.API.Models.Responses;

public sealed class CompanyDto
{
    public long Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Email { get; init; }
    public string? WebsiteUrl { get; init; }
    public bool HasApiCredentials { get; init; }
    public string? LanguageCode { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }

    public static CompanyDto From(Company c) => new()
    {
        Id                 = c.Id,
        Name               = c.Name,
        Email              = c.Email,
        WebsiteUrl         = c.WebsiteUrl,
        HasApiCredentials  = !string.IsNullOrEmpty(c.IkasApiKey),
        LanguageCode       = c.LanguageCode,
        IsActive           = c.IsActive,
        CreatedDate        = c.CreatedDate,
        UpdatedDate        = c.UpdatedDate
    };
}
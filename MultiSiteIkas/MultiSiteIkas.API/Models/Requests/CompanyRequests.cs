using System.ComponentModel.DataAnnotations;

namespace MultiSiteIkas.API.Models.Requests;

public sealed class CreateCompanyRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = null!;

    [EmailAddress, MaxLength(200)]
    public string? Email { get; init; }

    [MaxLength(500)]
    public string? WebsiteUrl { get; init; }

    [MaxLength(500)]
    public string? IkasApiKey { get; init; }

    [MaxLength(500)]
    public string? IkasApiSecret { get; init; }

    [MaxLength(10)]
    public string? LanguageCode { get; init; } = "tr";

    public bool IsActive { get; init; } = true;
}

public sealed class UpdateCompanyRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = null!;

    [EmailAddress, MaxLength(200)]
    public string? Email { get; init; }

    [MaxLength(500)]
    public string? WebsiteUrl { get; init; }

    [MaxLength(500)]
    public string? IkasApiKey { get; init; }

    [MaxLength(500)]
    public string? IkasApiSecret { get; init; }

    [MaxLength(10)]
    public string? LanguageCode { get; init; }

    public bool IsActive { get; init; }
}
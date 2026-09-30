using System.ComponentModel.DataAnnotations;

namespace MultiSiteIkas.API.Models.Requests;

public sealed class LoginRequest
{
    [Required] public string Username { get; init; } = null!;
    [Required] public string Password { get; init; } = null!;
}

public sealed class CreateUserRequest
{
    [Required] [MinLength(3)] public string Username { get; init; } = null!;
    [Required] [MinLength(6)] public string Password { get; init; } = null!;
    [Required] public string Role { get; init; } = "customer";
    public long? CompanyId { get; init; }
}

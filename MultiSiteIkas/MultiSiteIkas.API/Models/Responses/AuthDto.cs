namespace MultiSiteIkas.API.Models.Responses;

public sealed class LoginResponse
{
    public string Token { get; init; } = null!;
    public DateTime ExpiresAt { get; init; }
    public string Username { get; init; } = null!;
    public string Role { get; init; } = null!;
    public long? CompanyId { get; init; }
}

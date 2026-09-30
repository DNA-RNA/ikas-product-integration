using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MultiSiteIkas.Data.Entities;

namespace MultiSiteIkas.API.Infrastructure;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateToken(AppUser user);
}

public sealed class JwtService(IConfiguration config) : IJwtService
{
    public (string Token, DateTime ExpiresAt) GenerateToken(AppUser user)
    {
        var secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryHours = config.GetValue<int>("Jwt:ExpiryHours", 8);
        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Role, user.Role),
        };

        if (user.CompanyId.HasValue)
            claims.Add(new Claim("company_id", user.CompanyId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer:   config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:   claims,
            expires:  expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

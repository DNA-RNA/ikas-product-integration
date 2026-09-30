using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.API.Infrastructure;
using MultiSiteIkas.API.Models.Requests;
using MultiSiteIkas.API.Models.Responses;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IAppUserRepository users,
    IJwtService jwt) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var user = await users.GetByUsernameAsync(req.Username, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı" });

        var (token, expiresAt) = jwt.GenerateToken(user);
        return Ok(new LoginResponse
        {
            Token     = token,
            ExpiresAt = expiresAt,
            Username  = user.Username,
            Role      = user.Role,
            CompanyId = user.CompanyId
        });
    }

    [HttpPost("users")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (req.Role != "admin" && req.Role != "customer")
            return BadRequest(new { message = "Role 'admin' veya 'customer' olmalıdır" });

        var existing = await users.GetByUsernameAsync(req.Username, ct);
        if (existing is not null)
            return Conflict(new { message = "Bu kullanıcı adı zaten kullanılıyor" });

        var id = await users.CreateAsync(new AppUser
        {
            Username     = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role         = req.Role,
            CompanyId    = req.CompanyId,
            IsActive     = true
        }, ct);

        return Ok(new { id, username = req.Username, role = req.Role });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            id        = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            username  = User.Identity?.Name,
            role      = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
            companyId = User.FindFirst("company_id")?.Value
        });
    }
}

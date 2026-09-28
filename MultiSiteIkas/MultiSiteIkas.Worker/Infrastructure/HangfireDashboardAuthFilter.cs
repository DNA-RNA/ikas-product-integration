using System.Text;
using Hangfire.Dashboard;

namespace MultiSiteIkas.Worker.Infrastructure;

/// <summary>
/// Hangfire dashboard'u Basic Auth ile korur.
/// - Development: sadece localhost erişir (DashboardUser boşsa)
/// - Production: appsettings.Production.json'daki credentials zorunlu
/// </summary>
public class HangfireDashboardAuthFilter(IConfiguration config) : IDashboardAuthorizationFilter
{
    private readonly string? _user = config["Hangfire:DashboardUser"];
    private readonly string? _pass = config["Hangfire:DashboardPassword"];

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();

        // Credentials tanımlı değilse sadece localhost'a izin ver (geliştirici ortamı)
        if (string.IsNullOrEmpty(_user))
            return IsLocalhost(http);

        var header = http.Request.Headers.Authorization.ToString();

        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return Challenge(http);

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            var sep = decoded.IndexOf(':');
            if (sep < 0) return false;
            return decoded[..sep] == _user && decoded[(sep + 1)..] == _pass;
        }
        catch { return false; }
    }

    private static bool IsLocalhost(HttpContext http)
    {
        var remote = http.Connection.RemoteIpAddress;
        return remote is null
            || remote.Equals(System.Net.IPAddress.Loopback)
            || remote.Equals(System.Net.IPAddress.IPv6Loopback);
    }

    private static bool Challenge(HttpContext http)
    {
        http.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire — Developer Access\"";
        http.Response.StatusCode = 401;
        return false;
    }
}

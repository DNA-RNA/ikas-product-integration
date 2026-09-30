using System.Text;
using Dapper;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using MultiSiteIkas.API.Infrastructure;
using MultiSiteIkas.Core.Caching;
using MultiSiteIkas.Core.Ikas;
using MultiSiteIkas.Core.Interfaces;
using MultiSiteIkas.Core.Services;
using MultiSiteIkas.Core.Transfer;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;
using MultiSiteIkas.Data.Repositories;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(config)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ── CORS ────────────────────────────────────────────────────────────────────
var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── JWT Authentication ───────────────────────────────────────────────────────
var jwtSecret = config["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = config["Jwt:Issuer"],
            ValidAudience            = config["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// ── Cache ─────────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// ── Data layer ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDbConnectionFactory>(new PostgresConnectionFactory(connectionString));
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<IXmlSourceRepository, XmlSourceRepository>();
builder.Services.AddScoped<ISiteMappingRepository, SiteMappingRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductTransferRepository, ProductTransferRepository>();
builder.Services.AddScoped<ITransferLogRepository, TransferLogRepository>();

// ── Core services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<ICategoryFilterService, CategoryFilterService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IIkasFieldMapper, IkasFieldMapper>();
builder.Services.AddScoped<ICategoryResolver, CategoryResolver>();
builder.Services.AddScoped<ITransferService, TransferService>();

// ── JWT service ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IJwtService, JwtService>();

// ── HTTP clients ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IIkasApiService, IkasApiService>(client =>
{
    client.BaseAddress = new Uri("https://api.myikas.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IXmlParsingService, MultiSiteIkas.Core.Xml.XmlParsingService>();
builder.Services.AddScoped<IXmlPullService, MultiSiteIkas.Core.Xml.XmlPullService>();
builder.Services.AddHttpClient("XmlDownloader", client => { client.Timeout = TimeSpan.FromMinutes(5); });

// ── Hangfire CLIENT (job kuyruğa atmak için — server Worker'da) ─────────────
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

// ── API ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MultiSite İkas API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT token'ını gir (sadece token, 'Bearer' yazmana gerek yok)",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

var app = builder.Build();

// ── Admin user seeding (first run) ──────────────────────────────────────────
await SeedAdminAsync(app);

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "Multi-Site İkas Product Integration API v1.0");
app.MapGet("/health", async (IDbConnectionFactory db) =>
{
    try
    {
        using var conn = db.CreateConnection();
        await conn.QuerySingleAsync<int>("SELECT 1");
        return Results.Ok(new { status = "healthy", service = "api", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", error = ex.Message }, statusCode: 503);
    }
});

try
{
    Log.Information("Starting Multi-Site İkas API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// ── Admin seed helper ───────────────────────────────────────────────────────
static async Task SeedAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
    var cfg   = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var adminUsername = cfg["Seed:AdminUsername"] ?? "admin";
    var existing = await users.GetByUsernameAsync(adminUsername);
    if (existing is not null) return;

    var password = cfg["Seed:AdminPassword"] ?? "Admin123!";
    await users.CreateAsync(new AppUser
    {
        Username     = adminUsername,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role         = "admin",
        IsActive     = true
    });

    Log.Information("Admin kullanıcı oluşturuldu: {Username} — ilk girişte şifreyi değiştir!", adminUsername);
}

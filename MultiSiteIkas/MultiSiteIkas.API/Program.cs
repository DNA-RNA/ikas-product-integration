using Dapper;
using Hangfire;
using Hangfire.PostgreSql;
using Serilog;
using MultiSiteIkas.Core.Caching;
using MultiSiteIkas.Core.Ikas;
using MultiSiteIkas.Core.Interfaces;
using MultiSiteIkas.Core.Services;
using MultiSiteIkas.Core.Transfer;
using MultiSiteIkas.Data.Connections;
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

// ── Cache (IMemoryCache → MemoryCacheService → ICacheService) ───────────────
// Redis version: AddStackExchangeRedisCache + RedisDistributedCacheService
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// ── Data layer ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDbConnectionFactory>(new PostgresConnectionFactory(connectionString));
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

// ── HTTP clients ────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IIkasApiService, IkasApiService>(client =>
{
    client.BaseAddress = new Uri("https://api.myikas.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IXmlParsingService, MultiSiteIkas.Core.Xml.XmlParsingService>();
builder.Services.AddScoped<IXmlPullService, MultiSiteIkas.Core.Xml.XmlPullService>();
builder.Services.AddHttpClient("XmlDownloader", client => { client.Timeout = TimeSpan.FromMinutes(5); });

// ── Hangfire CLIENT (sadece job kuyruğa atmak için — server Worker'da) ──────
// AddHangfire storage'ı tanıtır; IBackgroundJobClient artık inject edilebilir
// AddHangfireServer ÇAĞRILMIYOR — job'ları Worker çalıştıracak
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

// ── API ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "Multi-Site İkas Product Integration API v1.0");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "api", timestamp = DateTime.UtcNow }));

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
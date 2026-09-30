using Dapper;
using Hangfire;
using Hangfire.PostgreSql;
using Serilog;
using MultiSiteIkas.Core.Caching;
using MultiSiteIkas.Worker.Infrastructure;
using MultiSiteIkas.Core.Ikas;
using MultiSiteIkas.Core.Interfaces;
using MultiSiteIkas.Core.Services;
using MultiSiteIkas.Core.Transfer;
using MultiSiteIkas.Data.Connections;
using MultiSiteIkas.Data.Interfaces;
using MultiSiteIkas.Data.Repositories;
using MultiSiteIkas.Jobs;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ── Logging ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(config)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ── Cache ─────────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// ── Data layer ────────────────────────────────────────────────────────────────
// Job'lar DB'ye yazar: ürün transferi, log kaydı vb. — repository'lerin tümü gerekli
builder.Services.AddSingleton<IDbConnectionFactory>(new PostgresConnectionFactory(connectionString));
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<IXmlSourceRepository, XmlSourceRepository>();
builder.Services.AddScoped<ISiteMappingRepository, SiteMappingRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductTransferRepository, ProductTransferRepository>();
builder.Services.AddScoped<ITransferLogRepository, TransferLogRepository>();

// ── Core services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICategoryFilterService, CategoryFilterService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IIkasFieldMapper, IkasFieldMapper>();
builder.Services.AddScoped<ICategoryResolver, CategoryResolver>();
builder.Services.AddScoped<ITransferService, TransferService>();

// ── HTTP clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IIkasApiService, IkasApiService>(client =>
{
    client.BaseAddress = new Uri("https://api.myikas.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IXmlParsingService, MultiSiteIkas.Core.Xml.XmlParsingService>();
builder.Services.AddScoped<IXmlPullService, MultiSiteIkas.Core.Xml.XmlPullService>();
builder.Services.AddHttpClient("XmlDownloader", client => { client.Timeout = TimeSpan.FromMinutes(5); });

// ── Job sınıfları (Hangfire DI için) ─────────────────────────────────────────
builder.Services.AddScoped<XmlPullJob>();
builder.Services.AddScoped<TransferJob>();
builder.Services.AddScoped<OrchestratorJob>();

// ── Hangfire: sunucu + PostgreSQL storage ─────────────────────────────────────
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString), new PostgreSqlStorageOptions
    {
        PrepareSchemaIfNecessary = true,
        QueuePollInterval        = TimeSpan.FromSeconds(15),
        InvisibilityTimeout      = TimeSpan.FromMinutes(30),
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = config.GetValue<int>("Hangfire:WorkerCount", 5);
    options.Queues      = ["orchestrator", "xml-pull", "transfer", "default"];
});

builder.Services.AddSingleton<HangfireDashboardAuthFilter>();

var app = builder.Build();

// ── Hangfire Dashboard ────────────────────────────────────────────────────────
// Sadece geliştiriciler erişir; prod'da Basic Auth zorunlu
app.UseHangfireDashboard(
    config["Hangfire:DashboardPath"] ?? "/hangfire",
    new DashboardOptions
    {
        Authorization  = [app.Services.GetRequiredService<HangfireDashboardAuthFilter>()],
        DashboardTitle = "MultiSite İkas — Job Dashboard",
    });

// ── Recurring Jobs ────────────────────────────────────────────────────────────
// Her başlangıçta schedule yeniden kaydedilir — idempotent
var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();

if (config.GetValue<bool>("JobSchedule:XmlPullJob:Enabled"))
    recurringJobs.AddOrUpdate<OrchestratorJob>(
        "xml-pull-all",
        job => job.RunXmlPullAllAsync(CancellationToken.None),
        config["JobSchedule:XmlPullJob:CronExpression"] ?? "0 2 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

if (config.GetValue<bool>("JobSchedule:TransferJob:Enabled"))
    recurringJobs.AddOrUpdate<OrchestratorJob>(
        "transfer-all",
        job => job.RunTransferAllAsync(CancellationToken.None),
        config["JobSchedule:TransferJob:CronExpression"] ?? "0 6 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "worker", timestamp = DateTime.UtcNow }));
app.MapGet("/", () => Results.Redirect("/hangfire"));

try
{
    Log.Information("Starting MultiSite İkas Worker...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

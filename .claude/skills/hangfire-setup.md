# Hangfire Setup — .NET 8 + SQL Server

## NuGet Paketleri
```xml
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.*" />
<PackageReference Include="Hangfire.SqlServer" Version="1.8.*" />
```

## Program.cs — Kurulum

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddHangfire(config =>
{
    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        });
});

builder.Services.AddHangfireServer(opt =>
{
    opt.WorkerCount = Environment.ProcessorCount * 2;
    opt.Queues = new[] { "critical", "transfer", "pull", "default" };
    opt.ServerName = $"{Environment.MachineName}.{Environment.ProcessId}";
});

var app = builder.Build();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthFilter() },
    DashboardTitle = "Multi-Site İkas — Job Dashboard",
    StatsPollingInterval = 5000,
    DisplayStorageConnectionString = false,
    DefaultRecordsPerPage = 50
});
```

> Hangfire `HangFire` şeması altında tablolarını otomatik yaratır. İlk migration ile beraber gelir.

## Dashboard Authorization Filter

```csharp
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Geliştirme: localhost'tan herkes
        if (httpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip))
            return true;

        // Production: Basic Auth
        var header = httpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Challenge(httpContext);
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..]));
            var parts = decoded.Split(':', 2);
            if (parts.Length != 2) { Challenge(httpContext); return false; }

            var expectedUser = Environment.GetEnvironmentVariable("HANGFIRE_USERNAME") ?? "admin";
            var expectedPass = Environment.GetEnvironmentVariable("HANGFIRE_PASSWORD");

            if (string.IsNullOrEmpty(expectedPass))
            {
                // Prod'da env yoksa girilemesin
                return false;
            }

            return parts[0] == expectedUser && parts[1] == expectedPass;
        }
        catch
        {
            Challenge(httpContext);
            return false;
        }
    }

    private static void Challenge(HttpContext ctx)
    {
        ctx.Response.StatusCode = 401;
        ctx.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire\"";
    }
}
```

## Recurring Job Registration

`Program.cs` sonunda (veya ayrı bir `IHostedService` ile):

```csharp
using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    var schedule = scope.ServiceProvider.GetRequiredService<IOptions<JobScheduleOptions>>().Value;

    // XML Pull (master mağazadan veri çekme)
    recurringJobs.AddOrUpdate<IXmlPullJob>(
        recurringJobId: "xml-pull",
        queue: "pull",
        methodCall: j => j.ExecuteAsync(CancellationToken.None),
        cronExpression: schedule.XmlPullCron,
        options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    // Her hedef mağaza için transfer
    foreach (var transferJob in schedule.TransferJobs)
    {
        recurringJobs.AddOrUpdate<ITransferJob>(
            recurringJobId: $"transfer-mapping-{transferJob.SiteMappingId}",
            queue: "transfer",
            methodCall: j => j.ExecuteAsync(transferJob.SiteMappingId, CancellationToken.None),
            cronExpression: transferJob.Cron,
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }

    // Health Check
    recurringJobs.AddOrUpdate<IHealthCheckJob>(
        recurringJobId: "health-check",
        queue: "default",
        methodCall: j => j.ExecuteAsync(CancellationToken.None),
        cronExpression: schedule.HealthCheckCron);
}
```

## Job Interface'leri ve Sınıfları

```csharp
public interface IXmlPullJob
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    Task ExecuteAsync(CancellationToken ct);
}

public sealed class XmlPullJob(
    IXmlPullService xmlPullService,
    ILogger<XmlPullJob> logger) : IXmlPullJob
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("XML Pull job started");
        await xmlPullService.PullAllActiveSourcesAsync(ct);
        logger.LogInformation("XML Pull job completed");
    }
}

public interface ITransferJob
{
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 120, 600 })]
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    Task ExecuteAsync(long siteMappingId, CancellationToken ct);
}

public sealed class TransferJob(
    ITransferService transferService,
    ILogger<TransferJob> logger) : ITransferJob
{
    public async Task ExecuteAsync(long siteMappingId, CancellationToken ct)
    {
        logger.LogInformation("Transfer job started for SiteMapping {Id}", siteMappingId);
        var result = await transferService.RunTransferAsync(siteMappingId, ct);
        logger.LogInformation(
            "Transfer job completed for SiteMapping {Id}: {Success} success, {Errors} errors in {Sec}s",
            siteMappingId, result.SuccessCount, result.ErrorCount, result.Duration.TotalSeconds);
    }
}
```

## appsettings.json — Schedule Config

```json
{
  "JobSchedule": {
    "XmlPullCron": "0 2 * * *",
    "HealthCheckCron": "*/30 * * * *",
    "TransferJobs": [
      { "SiteMappingId": 1, "Cron": "30 2 * * *" },
      { "SiteMappingId": 2, "Cron": "0 3 * * *" },
      { "SiteMappingId": 3, "Cron": "30 3 * * *" },
      { "SiteMappingId": 4, "Cron": "0 4 * * *" }
    ]
  }
}
```

Cron örnekleri:
- `0 2 * * *` — her gün gece 2:00
- `0 */6 * * *` — her 6 saatte bir
- `*/30 * * * *` — her yarım saatte bir
- `0 0 * * 1` — her pazartesi gece yarısı

## Manual Job Tetikleme (Admin API)

```csharp
[ApiController]
[Route("api/admin/jobs")]
public sealed class JobsController(IBackgroundJobClient jobs) : ControllerBase
{
    [HttpPost("xml-pull")]
    public IActionResult TriggerXmlPull()
    {
        var jobId = jobs.Enqueue<IXmlPullJob>(j => j.ExecuteAsync(CancellationToken.None));
        return Ok(new { jobId });
    }

    [HttpPost("transfer/{siteMappingId:long}")]
    public IActionResult TriggerTransfer(long siteMappingId)
    {
        var jobId = jobs.Enqueue<ITransferJob>(j => j.ExecuteAsync(siteMappingId, CancellationToken.None));
        return Ok(new { jobId });
    }
}
```

## Best Practice'ler

1. **Job sınıfı INCE olsun**: gerçek iş `Core` servisinde, job sadece delegate eder.
2. **DisableConcurrentExecution** uzun süren job'larda (transfer, pull): aynı job ID iki kere paralel çalışmaz.
3. **CancellationToken** her job method'unda olmalı, dashboard'dan stop edildiğinde tetiklenir.
4. **AutomaticRetry**: API call başarısız olursa Hangfire seviyesinde retry. Polly'nin retry'ı ayrı katman (API call retry'ı).
5. **Logging**: Job ID, başlangıç, bitiş, süre, sonuç istatistikleri.
6. **Parameter JSON-serializable**: primitive, record, dictionary — DbContext veya HttpClient parametre olarak verme.

## Yasaklar
- Hardcoded schedule string'leri — config'ten oku.
- Job içinde uzun `Thread.Sleep` veya `Task.Delay` — onlar için recurring kullan.
- Job'ı sınıfın static metoduna bağlama — DI alamaz, test edilemez.
- Production'da Hangfire dashboard'u auth'suz açma. Asla.

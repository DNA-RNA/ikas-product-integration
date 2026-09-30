using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.API.Models.Responses;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.API.Controllers;

[ApiController]
[Route("api/transfer-logs")]
[Authorize]
public class TransferLogsController(ITransferLogRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? siteMappingId = null,
        [FromQuery] long? xmlSourceId = null,
        [FromQuery] long? targetCompanyId = null,
        [FromQuery] byte? status = null,        // 0=Pending 1=Success 2=Failed
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
    {
        // Müşteri sadece kendi şirketinin loglarını görür
        if (!User.IsInRole("admin") && User.FindFirst("company_id")?.Value is string v)
            targetCompanyId = long.Parse(v);

        var result = await repo.GetPagedAsync(
            page, pageSize, siteMappingId, xmlSourceId, targetCompanyId, status, sortField, sortDir, ct);

        return Ok(PagedResponse<TransferLogDto>.From(
            (result.Items.Select(TransferLogDto.From), result.TotalCount), page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var log = await repo.GetByIdAsync(id, ct);
        return log is null ? NotFound() : Ok(TransferLogDto.From(log));
    }

    [HttpGet("failures")]
    public async Task<IActionResult> GetRecentFailures(
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var logs = await repo.GetRecentFailuresAsync(take, ct);
        return Ok(logs.Select(TransferLogDto.From));
    }
}
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.API.Models.Requests;
using MultiSiteIkas.API.Models.Responses;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;
using MultiSiteIkas.Jobs;

namespace MultiSiteIkas.API.Controllers;

[ApiController]
[Route("api/xml-sources")]
[Authorize(Roles = "admin")]
public class XmlSourcesController(
    IXmlSourceRepository repo,
    IBackgroundJobClient jobs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
    {
        var result = await repo.GetPagedAsync(page, pageSize, search, isActive, sortField, sortDir, ct);
        return Ok(PagedResponse<XmlSourceDto>.From(
            (result.Items.Select(XmlSourceDto.From), result.TotalCount), page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var source = await repo.GetByIdAsync(id, ct);
        return source is null ? NotFound() : Ok(XmlSourceDto.From(source));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateXmlSourceRequest req, CancellationToken ct)
    {
        var entity = new XmlSource
        {
            Name               = req.Name,
            SourceCompanyId    = req.SourceCompanyId,
            XmlUrl             = req.XmlUrl,
            IsActive           = req.IsActive,
            SyncFrequencyHours = req.SyncFrequencyHours
        };

        var id = await repo.CreateAsync(entity, ct);
        entity.Id = id;
        return CreatedAtAction(nameof(GetById), new { id }, XmlSourceDto.From(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateXmlSourceRequest req, CancellationToken ct)
    {
        var existing = await repo.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        existing.Name               = req.Name;
        existing.XmlUrl             = req.XmlUrl;
        existing.IsActive           = req.IsActive;
        existing.SyncFrequencyHours = req.SyncFrequencyHours;

        await repo.UpdateAsync(existing, ct);
        return Ok(XmlSourceDto.From(existing));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var exists = await repo.GetByIdAsync(id, ct);
        if (exists is null) return NotFound();

        await repo.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Hangfire'a xml-pull job'u atar. Sonucu /hangfire'dan takip et.
    /// </summary>
    [HttpPost("{id:long}/pull")]
    public async Task<IActionResult> EnqueuePull(long id, CancellationToken ct)
    {
        var source = await repo.GetByIdAsync(id, ct);
        if (source is null) return NotFound();

        var jobId = jobs.Enqueue<XmlPullJob>(j => j.ExecuteAsync(id, CancellationToken.None));
        return Accepted(new { jobId, xmlSourceId = id, message = "Job kuyruğa alındı" });
    }
}
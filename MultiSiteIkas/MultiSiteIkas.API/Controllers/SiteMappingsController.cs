using Hangfire;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.API.Models.Requests;
using MultiSiteIkas.API.Models.Responses;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;
using MultiSiteIkas.Jobs;

namespace MultiSiteIkas.API.Controllers;

[ApiController]
[Route("api/site-mappings")]
public class SiteMappingsController(
    ISiteMappingRepository repo,
    IBackgroundJobClient jobs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? targetCompanyId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
    {
        var result = await repo.GetPagedAsync(page, pageSize, targetCompanyId, isActive, sortField, sortDir, ct);
        return Ok(PagedResponse<SiteMappingDto>.From(
            (result.Items.Select(SiteMappingDto.From), result.TotalCount), page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var mapping = await repo.GetByIdAsync(id, ct);
        return mapping is null ? NotFound() : Ok(SiteMappingDto.From(mapping));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSiteMappingRequest req, CancellationToken ct)
    {
        var entity = new SiteMapping
        {
            XmlSourceId           = req.XmlSourceId,
            TargetCompanyId       = req.TargetCompanyId,
            PriceMarginPercentage = req.PriceMarginPercentage,
            AdditionalPrice       = req.AdditionalPrice,
            CurrencyOverride      = req.CurrencyOverride,
            CategoryFilters       = req.CategoryFilters,
            CategoryMappings      = req.CategoryMappings,
            BrandMappings         = req.BrandMappings,
            DeactivateZeroStock   = req.DeactivateZeroStock,
            SendImages            = req.SendImages,
            SyncCron              = req.SyncCron,
            IsActive              = req.IsActive
        };

        var id = await repo.CreateAsync(entity, ct);
        entity.Id = id;
        return CreatedAtAction(nameof(GetById), new { id }, SiteMappingDto.From(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSiteMappingRequest req, CancellationToken ct)
    {
        var existing = await repo.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        existing.PriceMarginPercentage = req.PriceMarginPercentage;
        existing.AdditionalPrice       = req.AdditionalPrice;
        existing.CurrencyOverride      = req.CurrencyOverride;
        existing.CategoryFilters       = req.CategoryFilters;
        existing.CategoryMappings      = req.CategoryMappings;
        existing.BrandMappings         = req.BrandMappings;
        existing.DeactivateZeroStock   = req.DeactivateZeroStock;
        existing.SendImages            = req.SendImages;
        existing.SyncCron              = req.SyncCron;
        existing.IsActive              = req.IsActive;

        await repo.UpdateAsync(existing, ct);
        return Ok(SiteMappingDto.From(existing));
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
    /// Bu mapping için Hangfire'a transfer job'u atar.
    /// </summary>
    [HttpPost("{id:long}/transfer")]
    public async Task<IActionResult> EnqueueTransfer(long id, CancellationToken ct)
    {
        var mapping = await repo.GetByIdAsync(id, ct);
        if (mapping is null) return NotFound();

        var jobId = jobs.Enqueue<TransferJob>(j => j.ExecuteAsync(id, CancellationToken.None));
        return Accepted(new { jobId, siteMappingId = id, message = "Transfer job kuyruğa alındı" });
    }

    /// <summary>
    /// Sadece kategori filtrelerini günceller — tüm mapping'i PUT etmek gerekmez.
    /// </summary>
    [HttpPatch("{id:long}/filters")]
    public async Task<IActionResult> UpdateFilters(
        long id,
        [FromBody] UpdateFiltersRequest req,
        CancellationToken ct)
    {
        var exists = await repo.GetByIdAsync(id, ct);
        if (exists is null) return NotFound();

        await repo.UpdateFiltersAsync(id, req.CategoryFilters, req.CategoryMappings, ct);
        return Ok(new { id, message = "Filtreler güncellendi" });
    }
}

public sealed class UpdateFiltersRequest
{
    public string? CategoryFilters { get; init; }
    public string? CategoryMappings { get; init; }
}
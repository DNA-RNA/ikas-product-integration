using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.API.Models.Requests;
using MultiSiteIkas.API.Models.Responses;
using MultiSiteIkas.Data.Entities;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.API.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize(Roles = "admin")]
public class CompaniesController(ICompanyRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await repo.GetPagedAsync(page, pageSize, search, isActive, ct);
        return Ok(PagedResponse<CompanyDto>.From(
            (result.Items.Select(CompanyDto.From), result.TotalCount), page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var company = await repo.GetByIdAsync(id, ct);
        return company is null ? NotFound() : Ok(CompanyDto.From(company));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest req, CancellationToken ct)
    {
        var entity = new Company
        {
            Name           = req.Name,
            Email          = req.Email,
            WebsiteUrl     = req.WebsiteUrl,
            IkasApiKey     = req.IkasApiKey,
            IkasApiSecret  = req.IkasApiSecret,
            LanguageCode   = req.LanguageCode ?? "tr",
            IsActive       = req.IsActive
        };

        var id = await repo.CreateAsync(entity, ct);
        entity.Id = id;
        return CreatedAtAction(nameof(GetById), new { id }, CompanyDto.From(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCompanyRequest req, CancellationToken ct)
    {
        var existing = await repo.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        existing.Name          = req.Name;
        existing.Email         = req.Email;
        existing.WebsiteUrl    = req.WebsiteUrl;
        existing.IkasApiKey    = req.IkasApiKey;
        existing.IkasApiSecret = req.IkasApiSecret;
        existing.LanguageCode  = req.LanguageCode;
        existing.IsActive      = req.IsActive;

        await repo.UpdateAsync(existing, ct);
        return Ok(CompanyDto.From(existing));
    }

    [HttpPatch("{id:long}/toggle-active")]
    public async Task<IActionResult> ToggleActive(long id, CancellationToken ct)
    {
        var existing = await repo.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        existing.IsActive = !existing.IsActive;
        await repo.UpdateAsync(existing, ct);
        return Ok(new { id, isActive = existing.IsActive });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var exists = await repo.GetByIdAsync(id, ct);
        if (exists is null) return NotFound();

        await repo.DeleteAsync(id, ct);
        return NoContent();
    }
}
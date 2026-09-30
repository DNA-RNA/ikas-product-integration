using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.API.Models.Responses;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.API.Controllers;

[ApiController]
[Route("api/product-transfers")]
[Authorize]
public class ProductTransfersController(IProductTransferRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? targetCompanyId = null,
        [FromQuery] long? siteMappingId = null,
        [FromQuery] byte? transferStatus = null,  // 0=Pending 1=Success 2=Failed
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
    {
        // Müşteri sadece kendi şirketine yapılan transferleri görür
        if (!User.IsInRole("admin"))
        {
            if (User.FindFirst("company_id")?.Value is string v)
                targetCompanyId = long.Parse(v);
        }

        var result = await repo.GetPagedAsync(page, pageSize, targetCompanyId, siteMappingId, transferStatus, sortField, sortDir, ct);
        return Ok(PagedResponse<ProductTransferDto>.From(
            (result.Items.Select(ProductTransferDto.From), result.TotalCount), page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var transfer = await repo.GetByIdAsync(id, ct);
        if (transfer is null) return NotFound();

        // Müşteri başkasının transfer kaydına erişemez
        if (!User.IsInRole("admin") && User.FindFirst("company_id")?.Value is string v)
        {
            if (transfer.TargetCompanyId != long.Parse(v))
                return Forbid();
        }

        return Ok(ProductTransferDto.From(transfer));
    }

    [HttpGet("failed")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetFailed(CancellationToken ct)
    {
        var items = await repo.GetFailedAsync(ct);
        return Ok(items.Select(ProductTransferDto.From));
    }
}

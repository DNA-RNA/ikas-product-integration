using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.API.Models.Responses;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController(IProductRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? xmlSourceId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
    {
        // Müşteri sadece kendi şirketinin kaynak ürünlerini göremez (ürünler master kaynaktan geliyor)
        // Admin her şeyi görür
        long? companyId = User.IsInRole("admin") ? null :
            User.FindFirst("company_id")?.Value is string v ? long.Parse(v) : null;

        var result = await repo.GetPagedAsync(page, pageSize, xmlSourceId, companyId, search, isActive, sortField, sortDir, ct);
        return Ok(PagedResponse<ProductDto>.From(
            (result.Items.Select(ProductDto.From), result.TotalCount), page, pageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(ProductDto.From(product));
    }
}

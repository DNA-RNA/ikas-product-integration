using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.API.Models.Responses;
using MultiSiteIkas.Data.Connections;

namespace MultiSiteIkas.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDbConnectionFactory dbFactory) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        long? companyId = null;
        if (!User.IsInRole("admin") && User.FindFirst("company_id")?.Value is string v)
            companyId = long.Parse(v);

        using var conn = dbFactory.CreateConnection();
        DashboardStatsDto stats;

        if (companyId is null)
        {
            stats = await conn.QuerySingleAsync<DashboardStatsDto>("""
                SELECT
                    (SELECT COUNT(*)::int  FROM companies)                                                       AS total_companies,
                    (SELECT COUNT(*)::int  FROM companies        WHERE is_active = TRUE)                         AS active_companies,
                    (SELECT COUNT(*)::int  FROM xml_sources      WHERE is_active = TRUE)                         AS active_xml_sources,
                    (SELECT COUNT(*)::int  FROM site_mappings    WHERE is_active = TRUE)                         AS active_site_mappings,
                    (SELECT COUNT(*)       FROM products          WHERE is_deleted = FALSE)                       AS total_products,
                    (SELECT COUNT(*)       FROM product_transfers)                                                AS total_transfers,
                    (SELECT COUNT(*)       FROM product_transfers WHERE transfer_status = 1)                      AS successful_transfers,
                    (SELECT COUNT(*)       FROM product_transfers WHERE transfer_status = 2)                      AS failed_transfers,
                    (SELECT MAX(end_date)  FROM transfer_logs     WHERE status = 1)                               AS last_transfer_date,
                    (SELECT COUNT(*)       FROM transfer_logs     WHERE DATE(start_date) = CURRENT_DATE AND status = 1) AS today_success_count,
                    (SELECT COUNT(*)       FROM transfer_logs     WHERE DATE(start_date) = CURRENT_DATE AND status = 2) AS today_failed_count
                """);
        }
        else
        {
            stats = await conn.QuerySingleAsync<DashboardStatsDto>("""
                SELECT
                    0::int  AS total_companies,
                    0::int  AS active_companies,
                    0::int  AS active_xml_sources,
                    (SELECT COUNT(*)::int  FROM site_mappings    WHERE target_company_id = @companyId AND is_active = TRUE)  AS active_site_mappings,
                    0       AS total_products,
                    (SELECT COUNT(*)       FROM product_transfers WHERE target_company_id = @companyId)                      AS total_transfers,
                    (SELECT COUNT(*)       FROM product_transfers WHERE target_company_id = @companyId AND transfer_status = 1) AS successful_transfers,
                    (SELECT COUNT(*)       FROM product_transfers WHERE target_company_id = @companyId AND transfer_status = 2) AS failed_transfers,
                    (SELECT MAX(end_date)  FROM transfer_logs     WHERE target_company_id = @companyId AND status = 1)       AS last_transfer_date,
                    (SELECT COUNT(*)       FROM transfer_logs     WHERE target_company_id = @companyId AND DATE(start_date) = CURRENT_DATE AND status = 1) AS today_success_count,
                    (SELECT COUNT(*)       FROM transfer_logs     WHERE target_company_id = @companyId AND DATE(start_date) = CURRENT_DATE AND status = 2) AS today_failed_count
                """, new { companyId });
        }

        return Ok(stats);
    }
}

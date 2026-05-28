using Microsoft.AspNetCore.Mvc;
using MultiSiteIkas.Core.Ikas;
using MultiSiteIkas.Core.Interfaces;
using MultiSiteIkas.Data.Interfaces;

namespace MultiSiteIkas.API.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly IXmlPullService _xmlPull;
    private readonly ITransferService _transfer;
    private readonly IXmlSourceRepository _xmlSources;
    private readonly ISiteMappingRepository _siteMappings;
    private readonly IProductRepository _products;
    private readonly ICompanyRepository _companies;
    private readonly IIkasApiService _ikasApi;

    public TestController(
        IXmlPullService xmlPull,
        ITransferService transfer,
        IXmlSourceRepository xmlSources,
        ISiteMappingRepository siteMappings,
        IProductRepository products,
        ICompanyRepository companies,
        IIkasApiService ikasApi)
    {
        _xmlPull = xmlPull;
        _transfer = transfer;
        _xmlSources = xmlSources;
        _siteMappings = siteMappings;
        _products = products;
        _companies = companies;
        _ikasApi = ikasApi;
    }

    /// <summary>
    /// XML URL'inden ham içeriğin ilk 3000 karakterini döner — yapıyı anlamak için.
    /// GET /api/test/xml-preview/1
    /// </summary>
    [HttpGet("xml-preview/{xmlSourceId:long}")]
    public async Task<IActionResult> XmlPreview(long xmlSourceId, CancellationToken ct)
    {
        var source = await _xmlSources.GetByIdAsync(xmlSourceId, ct);
        if (source is null) return NotFound();

        var http = HttpContext.RequestServices
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("XmlDownloader");

        using var response = await http.GetAsync(source.XmlUrl, ct);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "XML URL'e erişilemedi");

        var raw = await response.Content.ReadAsStringAsync(ct);
        var preview = raw.Length > 3000 ? raw[..3000] + "\n...(truncated)" : raw;
        return Content(preview, "text/xml");
    }

    /// <summary>
    /// XML kaynağını çek, parse et, products tablosuna upsert et.
    /// POST /api/test/xml-pull/1
    /// </summary>
    [HttpPost("xml-pull/{xmlSourceId:long}")]
    public async Task<IActionResult> XmlPull(long xmlSourceId, CancellationToken ct)
    {
        var result = await _xmlPull.PullAsync(xmlSourceId, ct);
        return result.IsSuccess ? Ok(result) : StatusCode(500, result);
    }

    /// <summary>
    /// Belirtilen site mapping için transfer çalıştır.
    /// POST /api/test/transfer/1
    /// </summary>
    [HttpPost("transfer/{siteMappingId:long}")]
    public async Task<IActionResult> RunTransfer(long siteMappingId, CancellationToken ct)
    {
        var result = await _transfer.RunTransferAsync(siteMappingId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Tüm xml_sources listesi — ID öğrenmek için.
    /// GET /api/test/xml-sources
    /// </summary>
    [HttpGet("xml-sources")]
    public async Task<IActionResult> GetXmlSources(CancellationToken ct)
    {
        var list = await _xmlSources.GetAllAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// Tüm site_mappings listesi — ID öğrenmek için.
    /// GET /api/test/site-mappings
    /// </summary>
    [HttpGet("site-mappings")]
    public async Task<IActionResult> GetSiteMappings(CancellationToken ct)
    {
        var list = await _siteMappings.GetAllActiveAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// Belirli xml_source için products tablosundaki ürünleri listele.
    /// GET /api/test/products/1
    /// </summary>
    [HttpGet("products/{xmlSourceId:long}")]
    public async Task<IActionResult> GetProducts(long xmlSourceId, CancellationToken ct)
    {
        var list = await _products.GetByXmlSourceIdAsync(xmlSourceId, ct);
        return Ok(new { count = list.Count(), products = list });
    }

    /// <summary>
    /// XML pull sonrası products tablosundaki benzersiz kategori yollarını listele.
    /// Kategori mapping yazmak için kullan.
    /// GET /api/test/product-categories/1
    /// </summary>
    [HttpGet("product-categories/{xmlSourceId:long}")]
    public async Task<IActionResult> GetProductCategories(long xmlSourceId, CancellationToken ct)
    {
        var products = await _products.GetByXmlSourceIdAsync(xmlSourceId, ct);
        var categories = products
            .Select(p => p.CategoryPath)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        return Ok(new { count = categories.Count, categories });
    }

    /// <summary>
    /// İkas v2 API şemasında createProduct / updateProduct mutation input tiplerini sorgular.
    /// GET /api/test/ikas-schema/{companyId}
    /// </summary>
    [HttpGet("ikas-schema/{companyId:long}")]
    public async Task<IActionResult> GetIkasSchema(long companyId, CancellationToken ct)
    {
        var company = await _companies.GetByIdAsync(companyId, ct);
        if (company is null) return NotFound();

        var creds = new MultiSiteIkas.Core.Ikas.IkasCredentials(
            company.IkasApiKey!, company.IkasApiSecret!, company.Name);

        const string introspection = """
            query {
              createVariant: __type(name: "CreateProductVariantInput") {
                name
                inputFields { name type { name kind ofType { name kind ofType { name kind } } } }
              }
              updateVariant: __type(name: "UpdateProductVariantInput") {
                name
                inputFields { name type { name kind ofType { name kind ofType { name kind } } } }
              }
              category: __type(name: "ProductCategoryInput") {
                name
                inputFields { name type { name kind ofType { name kind ofType { name kind } } } }
              }
              brand: __type(name: "ProductProductBrandInput") {
                name
                inputFields { name type { name kind ofType { name kind ofType { name kind } } } }
              }
              metaCreate: __type(name: "CreateHTMLMetaDataInput") {
                name
                inputFields { name type { name kind ofType { name kind ofType { name kind } } } }
              }
            }
            """;

        // Reflection ile internal SendAsync çağıramayız, HttpClient direkt kullanalım
        var http = HttpContext.RequestServices
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient();

        // Token al
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = company.IkasApiKey!,
            ["client_secret"] = company.IkasApiSecret!
        });
        using var tokenResp = await http.PostAsync("https://api.myikas.com/api/admin/oauth/token", form, ct);
        var tokenJson = await tokenResp.Content.ReadAsStringAsync(ct);
        var tokenDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
        var token = tokenDoc.RootElement.GetProperty("access_token").GetString();

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.myikas.com/api/v2/admin/graphql");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { query = introspection }),
            System.Text.Encoding.UTF8, "application/json");

        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        return Content(body, "application/json");
    }

    /// <summary>
    /// Hedef İkas mağazasının mevcut kategori listesini çek (dolceev.com gibi).
    /// category_mappings JSON'u yazmak için karşılaştır.
    /// GET /api/test/ikas-categories/3
    /// </summary>
    [HttpGet("ikas-categories/{companyId:long}")]
    public async Task<IActionResult> GetIkasCategories(long companyId, CancellationToken ct)
    {
        var company = await _companies.GetByIdAsync(companyId, ct);
        if (company is null) return NotFound($"Company {companyId} not found");
        if (string.IsNullOrEmpty(company.IkasApiKey))
            return BadRequest($"{company.Name} has no API credentials");

        var creds = new IkasCredentials(company.IkasApiKey, company.IkasApiSecret!, company.Name);
        var categories = await _ikasApi.ListCategoriesAsync(creds, ct);
        return Ok(new
        {
            store = company.Name,
            count = categories.Count,
            categories = categories.Select(c => new { c.Id, c.Name, c.ParentId })
        });
    }
}

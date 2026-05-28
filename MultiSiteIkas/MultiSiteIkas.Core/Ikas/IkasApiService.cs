using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MultiSiteIkas.Core.Exceptions;
using MultiSiteIkas.Core.Interfaces;

namespace MultiSiteIkas.Core.Ikas;

public sealed class IkasApiService : IIkasApiService
{
    private const string GraphqlEndpoint = "/api/v2/admin/graphql";
    private const string TokenEndpoint   = "/api/admin/oauth/token";

    private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresAt)> TokenCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILogger<IkasApiService> _logger;

    public IkasApiService(HttpClient http, ILogger<IkasApiService> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public async Task<IkasProduct?> FindBySkuAsync(IkasCredentials creds, string sku, CancellationToken ct = default)
    {
        const string query = """
            query FindProductBySku($sku: StringFilterInput) {
              listProduct(sku: $sku, pagination: { limit: 1, page: 1 }) {
                data { id name variants { id sku } }
                count
              }
            }
            """;

        var result = await SendAsync<ListProductData>(creds,
            new GraphQLRequest { Query = query, Variables = new { sku = new { eq = sku } } }, ct);

        return result.ListProduct.Data.FirstOrDefault();
    }

    public async Task<IkasProduct> SaveProductAsync(IkasCredentials creds, IkasProductInput input, CancellationToken ct = default)
    {
        return input.Id == null
            ? await CreateProductAsync(creds, input, ct)
            : await UpdateProductAsync(creds, input, ct);
    }

    public async Task<IReadOnlyList<IkasCategory>> ListCategoriesAsync(IkasCredentials creds, CancellationToken ct = default)
    {
        const string query = """
            query ListCategories {
              listCategory {
                id
                name
                parentId
              }
            }
            """;

        var result = await SendAsync<ListCategoryData>(creds,
            new GraphQLRequest { Query = query }, ct);

        return result.ListCategory;
    }

    // ── Create / Update ────────────────────────────────────────────────────────

    private async Task<IkasProduct> CreateProductAsync(IkasCredentials creds, IkasProductInput input, CancellationToken ct)
    {
        const string query = """
            mutation CreateProduct($input: CreateProductInput!) {
              createProduct(input: $input) {
                id name
                variants { id sku }
              }
            }
            """;

        var v2Input = new CreateProductInputV2
        {
            Name        = input.Name,
            Type        = input.Type,
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description,
            Weight      = input.Weight.HasValue && input.Weight > 0 ? (float)input.Weight.Value : null,
            Brand       = input.BrandName != null ? new BrandInputV2 { Name = input.BrandName } : null,
            Categories  = input.CategoryName != null
                ? new[] { new CategoryInputV2 { Name = input.CategoryName } }
                : null,
            Variants = input.Variants.Select(v => new CreateVariantInputV2
            {
                Sku              = v.Sku,
                IsActive         = v.IsActive,
                BarcodeList      = v.BarcodeList?.Length > 0 ? v.BarcodeList : null,
                Prices           = new[] { new PriceInputV2 { SellPrice = v.SellPrice } },
                Images           = v.Images.Length > 0
                    ? v.Images.Select(img => new VariantImageInputV2
                      {
                          ImageUrl = img.FileName,
                          Order    = img.Order,
                          IsMain   = img.IsMain
                      }).ToArray()
                    : null,
                Weight           = v.Weight,
                SellIfOutOfStock = true,
                VariantValues    = v.Attributes.Count > 0
                    ? v.Attributes.Select(kv => new VariantValueInput
                      {
                          VariantTypeName  = kv.Key,
                          VariantValueName = kv.Value
                      }).ToArray()
                    : null
            }).ToArray(),
            MetaData = input.MetaSlug != null ? new CreateMetaDataInputV2
            {
                Slug        = input.MetaSlug,
                PageTitle   = input.MetaTitle,
                Description = input.MetaDescription
            } : null
        };

        var result = await SendAsync<SaveProductData>(creds,
            new GraphQLRequest { Query = query, Variables = new { input = v2Input }, OperationName = "CreateProduct" }, ct);

        _logger.LogInformation("[{Store}] Ürün oluşturuldu: {Id}", creds.StoreCode, result.CreateProduct.Id);
        return result.CreateProduct;
    }

    private async Task<IkasProduct> UpdateProductAsync(IkasCredentials creds, IkasProductInput input, CancellationToken ct)
    {
        const string query = """
            mutation UpdateProduct($input: UpdateProductInput!) {
              updateProduct(input: $input) {
                id name
                variants { id sku }
              }
            }
            """;

        var updateVariants = input.Variants
            .Where(v => v.VariantId != null)
            .Select(v => new UpdateVariantInputV2
            {
                Id       = v.VariantId!,
                Sku      = v.Sku,
                IsActive = v.IsActive,
                Prices   = new[] { new PriceInputV2 { SellPrice = v.SellPrice } }
            }).ToArray();

        var v2Input = new UpdateProductInputV2
        {
            Id          = input.Id!,
            Name        = input.Name,
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description,
            Weight      = input.Weight.HasValue && input.Weight > 0 ? (float)input.Weight.Value : null,
            Brand       = input.BrandName != null ? new BrandInputV2 { Name = input.BrandName } : null,
            Categories  = input.CategoryName != null
                ? new[] { new CategoryInputV2 { Name = input.CategoryName } }
                : null,
            Variants    = updateVariants.Length > 0 ? updateVariants : null,
            MetaData    = input.MetaTitle != null ? new UpdateMetaDataInputV2
            {
                PageTitle   = input.MetaTitle,
                Description = input.MetaDescription
            } : null
        };

        var result = await SendAsync<UpdateProductData>(creds,
            new GraphQLRequest { Query = query, Variables = new { input = v2Input }, OperationName = "UpdateProduct" }, ct);

        _logger.LogInformation("[{Store}] Ürün güncellendi: {Id}", creds.StoreCode, result.UpdateProduct.Id);
        return result.UpdateProduct;
    }

    // ── OAuth2 token ───────────────────────────────────────────────────────────

    private async Task<string> GetAccessTokenAsync(IkasCredentials creds, CancellationToken ct)
    {
        if (TokenCache.TryGetValue(creds.ApiKey, out var cached) &&
            cached.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            return cached.Token;
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = creds.ApiKey,
            ["client_secret"] = creds.ApiSecret
        });

        using var tokenResponse = await _http.PostAsync(TokenEndpoint, form, ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var body = await tokenResponse.Content.ReadAsStringAsync(ct);
            throw new IkasApiException(creds.StoreCode, tokenResponse.StatusCode, $"Token hatası: {body}");
        }

        var tokenResult = await tokenResponse.Content
            .ReadFromJsonAsync<TokenResponse>(JsonOptions, ct)
            ?? throw new IkasApiException(creds.StoreCode, tokenResponse.StatusCode, "Boş token yanıtı");

        var expiresAt = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn);
        TokenCache[creds.ApiKey] = (tokenResult.AccessToken, expiresAt);

        _logger.LogInformation("[{Store}] Access token alındı, geçerlilik: {ExpiresAt:HH:mm:ss} UTC", creds.StoreCode, expiresAt);
        return tokenResult.AccessToken;
    }

    // ── GraphQL gönder ─────────────────────────────────────────────────────────

    private async Task<T> SendAsync<T>(IkasCredentials creds, GraphQLRequest request, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(creds, ct);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, GraphqlEndpoint)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[{Store}] HTTP {Code}: {Body}", creds.StoreCode, response.StatusCode, body);
            throw new IkasApiException(creds.StoreCode, response.StatusCode, body);
        }

        var gqlResponse = await response.Content
            .ReadFromJsonAsync<GraphQLResponse<T>>(JsonOptions, ct)
            ?? throw new IkasApiException(creds.StoreCode, response.StatusCode, "Boş yanıt");

        if (gqlResponse.Errors is { Count: > 0 })
        {
            var msg = string.Join("; ", gqlResponse.Errors.Select(e => e.Message));
            _logger.LogError("[{Store}] GraphQL hata: {Message}", creds.StoreCode, msg);
            throw new IkasApiException(creds.StoreCode, response.StatusCode, msg);
        }

        return gqlResponse.Data!;
    }
}

internal sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = null!;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; } = 14400;
}

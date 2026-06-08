using System.Net.Http.Json;
using System.Text.Json;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Modules.Payments.Infrastructure.Treasury;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper over the HU Treasury API. Registered
/// via <c>AddTreasuryIntegration</c>. Deserialisation is case-insensitive
/// (web defaults) so minor field-casing differences in the Treasury contract
/// do not break the mapping.
/// </summary>
public sealed class TreasuryClient : ITreasuryClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly TreasuryOptions _options;

    public TreasuryClient(HttpClient http, IOptions<TreasuryOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<TreasuryReceiptDto>> GetReceiptsAsync(CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(_options.ReceiptsPath)
            ? "api/payments/receipts"
            : _options.ReceiptsPath;

        var all = await _http.GetFromJsonAsync<List<TreasuryReceiptDto>>(path, Json, cancellationToken)
                  ?? new List<TreasuryReceiptDto>();

        // Only ConnectionTypeId == configured (6) receipts are relevant to the Portal.
        return all.Where(r => r.ConnectionTypeId == _options.ConnectionTypeId).ToList();
    }

    public async Task<TreasuryInitiateResponse> InitiateAsync(Gateway gateway, TreasuryInitiateRequest request, CancellationToken cancellationToken = default)
    {
        var path = TreasuryGatewayRoutes.InitiatePath(gateway);
        var response = await _http.PostAsJsonAsync(path, request, Json, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TreasuryInitiateResponse>(Json, cancellationToken);
        return body ?? throw new InvalidOperationException("Treasury returned an empty initiate response.");
    }
}

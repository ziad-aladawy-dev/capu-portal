using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Modules.Payments.Infrastructure.Treasury;

/// <summary>
/// Typed <see cref="HttpClient"/> over the HU Treasury API. Speaks each gateway's
/// distinct wire contract (request shape, response shape, BaseResponse wrapper)
/// and returns normalized results. Deserialisation uses web defaults
/// (camelCase, case-insensitive).
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

    // ── Receipts ────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<TreasuryReceiptDto>> GetReceiptsAsync(CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(_options.ReceiptsPath) ? "api/payments/receipts" : _options.ReceiptsPath;
        // connectionTypeIds is required by the contract (array). Send the single
        // configured type and still filter defensively.
        var url = $"{path}?connectionTypeIds={_options.ConnectionTypeId}";

        var envelope = await _http.GetFromJsonAsync<BaseResponse<List<TreasuryReceiptDto>>>(url, Json, cancellationToken);
        var data = envelope?.Data ?? new List<TreasuryReceiptDto>();
        return data.Where(r => r.ConnectionTypeId == _options.ConnectionTypeId).ToList();
    }

    // ── Initiation (per gateway) ────────────────────────────────────────────
    public async Task<TreasuryInitiateResult> InitiateAsync(Gateway gateway, TreasuryInitiateRequest request, CancellationToken cancellationToken = default)
    {
        var path = TreasuryGatewayRoutes.InitiatePath(gateway);

        switch (gateway)
        {
            case Gateway.BankMisr:
            {
                var body = new
                {
                    receiptIds = request.ReceiptIds,
                    studentReferenceId = request.StudentReferenceId,
                    billingDetails = MapBilling(request),
                    merchantRedirectUrl = request.RedirectUrl,
                };
                // BM wraps in { status, message, data }; success == "success".
                var env = await PostAsync<BmEnvelope<BmSessionData>>(path, body, cancellationToken);
                if (!string.Equals(env.Status, "success", StringComparison.OrdinalIgnoreCase) || env.Data is null)
                    throw new InvalidOperationException($"BM initiate failed: {env.Message}");
                // BM has no merchantOrderId in its session data; bmmId is the key.
                return new TreasuryInitiateResult
                {
                    MerchantOrderId = env.Data.BmmId ?? string.Empty,
                    RedirectUrl = env.Data.CheckoutUrl ?? string.Empty,
                };
            }
            case Gateway.Mastercard:
            {
                var body = new
                {
                    receiptIds = request.ReceiptIds,
                    studentReferenceId = request.StudentReferenceId,
                    merchantRedirectUrl = request.RedirectUrl,
                    currency = request.Currency,
                };
                var data = await PostUnwrapAsync<McInitiateData>(path, body, cancellationToken);
                return new TreasuryInitiateResult
                {
                    MerchantOrderId = data.MerchantOrderId ?? string.Empty,
                    RedirectUrl = data.RedirectUrl ?? string.Empty,
                };
            }
            case Gateway.EFinance:
            {
                var body = new
                {
                    receiptIds = request.ReceiptIds,
                    studentReferenceId = request.StudentReferenceId,
                    billingDetails = MapBilling(request),
                    paymentMechanism = string.IsNullOrWhiteSpace(request.PaymentMechanism) ? "NOT_SET" : request.PaymentMechanism,
                    description = request.Description ?? string.Empty,
                    redirectUrl = request.RedirectUrl,
                };
                var data = await PostUnwrapAsync<EfInitiateData>(path, body, cancellationToken);
                return new TreasuryInitiateResult
                {
                    MerchantOrderId = data.MerchantOrderId ?? string.Empty,
                    RedirectUrl = data.RedirectUrl ?? string.Empty,
                };
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(gateway), gateway, "Unsupported gateway.");
        }
    }

    // ── Status ──────────────────────────────────────────────────────────────
    public async Task<TreasuryStatusResult> GetStatusAsync(Gateway gateway, string merchantOrderId, CancellationToken cancellationToken = default)
    {
        var path = TreasuryGatewayRoutes.StatusPath(gateway, merchantOrderId);
        // Some gateways (e.g. eFinance) return data as an empty string / non-object
        // when there is nothing to report. Parse defensively: a non-object data is
        // treated as "not yet resolved" (empty status → Pending), never an exception.
        var envelope = await _http.GetFromJsonAsync<BaseResponse<JsonElement>>(path, Json, cancellationToken);
        if (envelope is null || envelope.Data.ValueKind != JsonValueKind.Object)
        {
            return new TreasuryStatusResult { MerchantOrderId = merchantOrderId, Status = string.Empty, Currency = "EGP" };
        }

        var data = envelope.Data.Deserialize<StatusData>(Json) ?? new StatusData();
        return new TreasuryStatusResult
        {
            MerchantOrderId = string.IsNullOrEmpty(data.MerchantOrderId) ? merchantOrderId : data.MerchantOrderId,
            Status = data.Status ?? string.Empty,
            Amount = ParseDecimal(data.Amount),
            Currency = string.IsNullOrEmpty(data.Currency) ? "EGP" : data.Currency,
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static object MapBilling(TreasuryInitiateRequest request) => new
    {
        firstName = request.Billing?.FirstName ?? string.Empty,
        lastName = request.Billing?.LastName ?? string.Empty,
        emailAddress = request.Billing?.EmailAddress ?? string.Empty,
        mobileNumber = request.Billing?.MobileNumber ?? string.Empty,
        currency = request.Billing?.Currency ?? request.Currency,
    };

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(path, body, Json, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return result ?? throw new InvalidOperationException($"Treasury returned an empty response for {path}.");
    }

    private async Task<T> PostUnwrapAsync<T>(string path, object body, CancellationToken ct)
    {
        var env = await PostAsync<BaseResponse<T>>(path, body, ct);
        if (!env.Success || env.Data is null)
            throw new InvalidOperationException($"Treasury request to {path} failed: {env.Message}");
        return env.Data;
    }

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    // ── Wire DTOs (internal) ────────────────────────────────────────────────
    private sealed class BaseResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    private sealed class BmEnvelope<T>
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    private sealed class BmSessionData
    {
        public string? SessionId { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? Token { get; set; }
        public string? BmmId { get; set; }
    }

    private sealed class McInitiateData
    {
        public string? MerchantOrderId { get; set; }
        public string? GatewaySessionId { get; set; }
        public string? SessionVersion { get; set; }
        public string? SuccessIndicator { get; set; }
        public string? RedirectUrl { get; set; }
    }

    private sealed class EfInitiateData
    {
        public string? MerchantOrderId { get; set; }
        public string? RedirectUrl { get; set; }
        public decimal? TotalAmount { get; set; }
        public decimal? BaseAmount { get; set; }
        public decimal? CollectionFees { get; set; }
        public string? Currency { get; set; }
        public string? ExpiryDate { get; set; }
    }

    private sealed class StatusData
    {
        public string? MerchantOrderId { get; set; }
        public string? Status { get; set; }
        public string? Amount { get; set; }
        public string? Currency { get; set; }
        public string? GatewayResult { get; set; }
    }
}

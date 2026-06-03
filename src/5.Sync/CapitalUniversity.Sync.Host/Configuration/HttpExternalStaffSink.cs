using System.Net.Http.Json;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Staff.Domain;
using CapitalUniversity.Sync.Staff.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Phase X.2 fix #3 — Staff sink counterpart to <see cref="HttpExternalStudentSink"/>.
/// Forwards the outbox row's stable Guid as the standard <c>Idempotency-Key</c>
/// HTTP header — see <see cref="HttpExternalStudentSink"/> for the rationale.
/// </summary>
public sealed class HttpExternalStaffSink : IExternalStaffSink
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpExternalStaffSink> _logger;

    public HttpExternalStaffSink(HttpClient httpClient, ILogger<HttpExternalStaffSink> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task PushAsync(ExternalStaff payload, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"staff/{Uri.EscapeDataString(payload.ExternalStaffId)}")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add(IdempotencyKeyHeader, idempotencyKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "HttpExternalStaffSink rejection. ExternalStaffId={Id} IdempotencyKey={Key} Status={Status} Body={Body}",
                payload.ExternalStaffId, idempotencyKey, (int)response.StatusCode, TextHelpers.Truncate(body, 500));

            throw new InvalidOperationException(
                $"HTTP push rejected for ExternalStaffId={payload.ExternalStaffId}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }
}

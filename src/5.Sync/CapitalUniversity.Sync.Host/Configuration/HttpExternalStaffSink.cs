using System.Net.Http.Json;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Staff.Domain;
using CapitalUniversity.Sync.Staff.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Phase X.2 fix #3 — Staff sink counterpart to <see cref="HttpExternalStudentSink"/>.
/// </summary>
public sealed class HttpExternalStaffSink : IExternalStaffSink
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpExternalStaffSink> _logger;

    public HttpExternalStaffSink(HttpClient httpClient, ILogger<HttpExternalStaffSink> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task PushAsync(ExternalStaff payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var response = await _httpClient.PutAsJsonAsync(
            $"staff/{Uri.EscapeDataString(payload.ExternalStaffId)}",
            payload,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "HttpExternalStaffSink rejection. ExternalStaffId={Id} Status={Status} Body={Body}",
                payload.ExternalStaffId, (int)response.StatusCode, TextHelpers.Truncate(body, 500));

            throw new InvalidOperationException(
                $"HTTP push rejected for ExternalStaffId={payload.ExternalStaffId}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

}
using System.Runtime.CompilerServices;
using System.Text.Json;
using CapitalUniversity.Sync.Staff.Domain;
using CapitalUniversity.Sync.Staff.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Phase X.2 fix #3 — Staff source counterpart to <see cref="HttpExternalStudentSource"/>.
/// </summary>
public sealed class HttpExternalStaffSource : IExternalStaffSource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpExternalStaffSource> _logger;

    public HttpExternalStaffSource(HttpClient httpClient, ILogger<HttpExternalStaffSource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async IAsyncEnumerable<ExternalStaff> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = sinceExclusive.HasValue
            ? $"staff?since={Uri.EscapeDataString(sinceExclusive.Value.ToString("O"))}"
            : "staff";

        // Same streaming-deserialization pattern as HttpExternalStudentSource;
        // memory bounded by the pipeline's BatchSize, not by upstream payload size.
        var response = await _httpClient
            .GetAsync(query, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "HttpExternalStaffSource HTTP error. Since={Since} Status={Status}",
                sinceExclusive, (int)response.StatusCode);
            throw new InvalidOperationException(
                $"Upstream Staff source returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = JsonSerializer.DeserializeAsyncEnumerable<ExternalStaff>(
            stream, cancellationToken: cancellationToken);

        await foreach (var s in items.ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (s is not null)
            {
                yield return s;
            }
        }
    }
}
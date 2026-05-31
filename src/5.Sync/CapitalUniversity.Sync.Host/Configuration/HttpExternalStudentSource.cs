using System.Runtime.CompilerServices;
using System.Text.Json;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Phase X.2 fix #3 — HTTP implementation of <see cref="IExternalStudentSource"/>.
/// Streams <c>GET {BaseUrl}/students?since={iso8601}</c> response as a JSON array
/// of <see cref="ExternalStudent"/> records.
///
/// <para>
/// Minimal scaffold — production deployments will want pagination, cursor
/// tokens, retry policies (e.g. Polly), authentication beyond a header API
/// key, etc. The shape proves the swap-in pattern: the module project never
/// changes; this implementation registers as <c>IExternalStudentSource</c>
/// after <c>AddStudentSync(...)</c> when the operator opts in.
/// </para>
/// </summary>
public sealed class HttpExternalStudentSource : IExternalStudentSource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpExternalStudentSource> _logger;

    public HttpExternalStudentSource(HttpClient httpClient, ILogger<HttpExternalStudentSource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async IAsyncEnumerable<ExternalStudent> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = sinceExclusive.HasValue
            ? $"students?since={Uri.EscapeDataString(sinceExclusive.Value.ToString("O"))}"
            : "students";

        // HttpCompletionOption.ResponseHeadersRead returns as soon as headers
        // arrive; the response body is consumed by the streaming deserializer
        // below. Pull-mode memory is bounded by the pipeline's BatchSize, not by
        // the upstream's response size — large responses can no longer OOM the
        // worker.
        var response = await _httpClient
            .GetAsync(query, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "HttpExternalStudentSource HTTP error. Since={Since} Status={Status}",
                sinceExclusive, (int)response.StatusCode);
            throw new InvalidOperationException(
                $"Upstream Student source returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = JsonSerializer.DeserializeAsyncEnumerable<ExternalStudent>(
            stream, cancellationToken: cancellationToken);

        await foreach (var student in items.ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (student is not null)
            {
                yield return student;
            }
        }
    }
}
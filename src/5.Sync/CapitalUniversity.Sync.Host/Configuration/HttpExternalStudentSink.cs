using System.Net.Http.Json;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Phase X.2 fix #3 — HTTP implementation of <see cref="IExternalStudentSink"/>.
/// Posts <c>POST {BaseUrl}/students/{externalStudentId}</c> with the JSON payload.
/// 2xx → success, any other status → throws so the push pipeline records the
/// failure on the outbox row (Phase 6 per-row failure isolation).
///
/// <para>
/// Lives in <c>Sync.Host</c> — not in <c>Sync.Student</c> — so the module
/// project stays byte-identical. Registered as <see cref="IExternalStudentSink"/>
/// AFTER <c>AddStudentSync(...)</c> in <c>Program.cs</c>; DI's last-wins
/// semantics make this the active sink when
/// <c>Sync:Integration:UseHttpAdapters = true</c>.
/// </para>
///
/// <para>
/// <b>Idempotency contract.</b> The platform model requires sinks to be
/// idempotent on the external merge key (here: <see cref="ExternalStudent.ExternalStudentId"/>).
/// PUT-like semantics are appropriate — the upstream MUST treat a re-presented
/// payload as an update rather than a duplicate.
/// </para>
/// </summary>
public sealed class HttpExternalStudentSink : IExternalStudentSink
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpExternalStudentSink> _logger;

    public HttpExternalStudentSink(HttpClient httpClient, ILogger<HttpExternalStudentSink> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task PushAsync(ExternalStudent payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var response = await _httpClient.PutAsJsonAsync(
            $"students/{Uri.EscapeDataString(payload.ExternalStudentId)}",
            payload,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "HttpExternalStudentSink rejection. ExternalStudentId={Id} Status={Status} Body={Body}",
                payload.ExternalStudentId, (int)response.StatusCode, TextHelpers.Truncate(body, 500));

            throw new InvalidOperationException(
                $"HTTP push rejected for ExternalStudentId={payload.ExternalStudentId}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

}
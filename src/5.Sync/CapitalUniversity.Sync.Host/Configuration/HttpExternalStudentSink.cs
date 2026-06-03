using System.Net.Http.Json;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Phase X.2 fix #3 — HTTP implementation of <see cref="IExternalStudentSink"/>.
/// Posts <c>PUT {BaseUrl}/students/{externalStudentId}</c> with the JSON payload.
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
/// <b>Idempotency contract.</b> The supplied <c>idempotencyKey</c> (the outbox
/// row's stable Guid) is forwarded as the standard <c>Idempotency-Key</c> HTTP
/// header — the convention used by Stripe / AWS / Twilio etc. A compliant
/// upstream returns 200/204 from its idempotency cache on a repeat, so a
/// SaveChanges-after-push crash on our side cannot produce a duplicate side
/// effect on theirs. PUT semantics on the merge-key URL remain belt-and-braces
/// in case the upstream doesn't honour the header.
/// </para>
/// </summary>
public sealed class HttpExternalStudentSink : IExternalStudentSink
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpExternalStudentSink> _logger;

    public HttpExternalStudentSink(HttpClient httpClient, ILogger<HttpExternalStudentSink> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task PushAsync(ExternalStudent payload, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"students/{Uri.EscapeDataString(payload.ExternalStudentId)}")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add(IdempotencyKeyHeader, idempotencyKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "HttpExternalStudentSink rejection. ExternalStudentId={Id} IdempotencyKey={Key} Status={Status} Body={Body}",
                payload.ExternalStudentId, idempotencyKey, (int)response.StatusCode, TextHelpers.Truncate(body, 500));

            throw new InvalidOperationException(
                $"HTTP push rejected for ExternalStudentId={payload.ExternalStudentId}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }
}

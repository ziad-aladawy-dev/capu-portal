using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Modules.Payments.Abstractions.Events;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Repositories;

namespace CapitalUniversity.Modules.StudentServices.Application.Outbox;

/// <summary>
/// Bridges the Payments module's <c>payments.invoice.paid</c> outbox row into
/// the request lifecycle. When an invoice settles, every request whose
/// <c>PaymentReferenceId</c> matches is advanced out of <c>WaitingPayment</c>
/// through <see cref="IStudentServiceRequestService.ConfirmPaymentAsync"/>.
///
/// <para>
/// At-least-once delivery: handlers must be idempotent. Two safeguards:
///   <list type="number">
///     <item><see cref="IStudentServiceRequestService.ConfirmPaymentAsync"/>
///       no-ops on requests already past <c>WaitingPayment</c> — so replays
///       on a request that already advanced are silent.</item>
///     <item><see cref="IStudentServiceRequestRepository.GetByPaymentReferenceAsync"/>
///       returns an empty list for invoices not tied to any student-service
///       request — so unrelated invoice payments (from other modules using
///       the Payments seam) are silent no-ops here.</item>
///   </list>
/// </para>
///
/// <para>
/// This handler replaces the previous manual <c>POST /confirm-payment</c>
/// ops dance. The endpoint stays mounted as a break-glass tool, but the
/// happy path is now driven entirely by the outbox.
/// </para>
/// </summary>
public sealed class InvoicePaidEventHandler : IOutboxMessageHandler
{
    public string MessageType => InvoicePaidEvent.TypeKey;

    private readonly IStudentServiceRequestRepository _requests;
    private readonly IStudentServiceRequestService _requestService;
    private readonly IAppLogger _logger;

    public InvoicePaidEventHandler(
        IStudentServiceRequestRepository requests,
        IStudentServiceRequestService requestService,
        IAppLogger logger)
    {
        _requests = requests;
        _requestService = requestService;
        _logger = logger;
    }

    public async Task HandleAsync(Guid outboxMessageId, string payload, CancellationToken cancellationToken)
    {
        var fact = JsonSerializer.Deserialize<InvoicePaidFact>(payload)
            ?? throw new InvalidOperationException($"{nameof(InvoicePaidEventHandler)}: payload deserialised to null.");

        var matched = await _requests.GetByPaymentReferenceAsync(fact.InvoiceId, cancellationToken);

        // No requests are tied to this invoice — typically because the
        // invoice was authored by a different module's flow (e.g. tuition).
        // Log at info so the audit trail captures the routing decision without
        // alarming on it.
        if (matched.Count == 0)
        {
            await _logger.LogInfoAsync(
                $"{InvoicePaidEvent.TypeKey} no student-service requests matched invoice {fact.InvoiceId}",
                nameof(InvoicePaidEventHandler),
                context: null,
                metadata: new Dictionary<string, object>
                {
                    ["InvoiceId"] = fact.InvoiceId,
                    ["StudentId"] = fact.StudentId,
                });
            return;
        }

        foreach (var request in matched)
        {
            // ConfirmPaymentAsync is idempotent — already-advanced requests
            // log a no-op and return. We do NOT catch here because any
            // exception (e.g. transient DB failure) should bubble up so the
            // outbox dispatcher retries with backoff. Permanent-poison
            // detection lives in the dispatcher's MaxAttempts policy.
            await _requestService.ConfirmPaymentAsync(request.Id, cancellationToken);
        }

        await _logger.LogInfoAsync(
            $"{InvoicePaidEvent.TypeKey} advanced {matched.Count} request(s) on invoice {fact.InvoiceId}",
            nameof(InvoicePaidEventHandler),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["InvoiceId"] = fact.InvoiceId,
                ["StudentId"] = fact.StudentId,
                ["RequestCount"] = matched.Count,
            });
    }
}

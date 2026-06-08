using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.Events;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Repositories;

namespace CapitalUniversity.Modules.StudentServices.Application.Outbox;

/// <summary>
/// Bridges the Treasury <c>payments.fee.paid</c> outbox row into the request
/// lifecycle. A request's <c>PaymentReferenceId</c> holds the StudentFee id
/// (set at submit for the Treasury fee path), so matching by
/// <see cref="FeePaidFact.FeeId"/> advances the right request out of
/// <c>WaitingPayment</c>. Mirrors <see cref="InvoicePaidEventHandler"/>; both
/// are idempotent (ConfirmPaymentAsync no-ops on already-advanced requests).
/// </summary>
public sealed class FeePaidEventHandler : IOutboxMessageHandler
{
    public string MessageType => FeePaidEvent.TypeKey;

    private readonly IStudentServiceRequestRepository _requests;
    private readonly IStudentServiceRequestService _requestService;
    private readonly IAppLogger _logger;

    public FeePaidEventHandler(
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
        var fact = JsonSerializer.Deserialize<FeePaidFact>(payload)
            ?? throw new InvalidOperationException($"{nameof(FeePaidEventHandler)}: payload deserialised to null.");

        // Request.PaymentReferenceId == the fee id for the Treasury fee path.
        var matched = await _requests.GetByPaymentReferenceAsync(fact.FeeId, cancellationToken);
        if (matched.Count == 0)
        {
            await _logger.LogInfoAsync(
                $"{FeePaidEvent.TypeKey} no student-service requests matched fee {fact.FeeId}",
                nameof(FeePaidEventHandler),
                context: null,
                metadata: new Dictionary<string, object>
                {
                    ["FeeId"] = fact.FeeId,
                    ["StudentId"] = fact.StudentId,
                });
            return;
        }

        foreach (var request in matched)
        {
            await _requestService.ConfirmPaymentAsync(request.Id, cancellationToken);
        }

        await _logger.LogInfoAsync(
            $"{FeePaidEvent.TypeKey} advanced {matched.Count} request(s) on fee {fact.FeeId}",
            nameof(FeePaidEventHandler),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["FeeId"] = fact.FeeId,
                ["StudentId"] = fact.StudentId,
                ["RequestCount"] = matched.Count,
            });
    }
}

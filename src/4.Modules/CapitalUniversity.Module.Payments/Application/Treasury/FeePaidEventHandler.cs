using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.Events;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Drains the <c>payments.fee.paid</c> outbox stream so settlements do not
/// dead-letter (B7). <see cref="SettlementService"/> publishes one
/// <see cref="FeePaidFact"/> per fee on every settlement (webhook + reconciliation
/// paths). Without a handler whose <see cref="MessageType"/> equals
/// <see cref="FeePaidEvent.TypeKey"/>, the dispatcher retries each message
/// <c>Outbox:MaxAttempts</c> times and then flags it poisoned — on EVERY payment.
///
/// <para>
/// The intended downstream consumer (StudentServices advancing the originating
/// request to Paid) is not yet wired: fee creation does not currently populate
/// <c>SourceModule</c>/<c>SourceReferenceId</c> with a StudentServices request, so
/// there is nothing to resolve against today. This handler therefore acknowledges
/// and logs the fact only. It writes no database state, so it is trivially
/// idempotent under the outbox's at-least-once delivery.
/// </para>
///
/// <para>
/// TODO: when the fee-authoring linkage exists (producer sets
/// <c>SourceModule</c>/<c>SourceReferenceId</c>), replace the log-only body with
/// real consumption that advances the originating request — keyed on
/// <paramref name="outboxMessageId"/> for idempotency once it performs writes.
/// </para>
/// </summary>
public sealed class FeePaidEventHandler : IOutboxMessageHandler
{
    private readonly ILogger<FeePaidEventHandler> _logger;

    public FeePaidEventHandler(ILogger<FeePaidEventHandler> logger)
    {
        _logger = logger;
    }

    public string MessageType => FeePaidEvent.TypeKey;

    public Task HandleAsync(Guid outboxMessageId, string payload, CancellationToken cancellationToken)
    {
        var fact = JsonSerializer.Deserialize<FeePaidFact>(payload)
                   ?? throw new InvalidOperationException("FeePaidEventHandler: payload deserialised to null.");

        _logger.LogInformation(
            "FeePaidEvent drained: fee={FeeId} order={OrderId} student={StudentId} " +
            "source={SourceModule}/{SourceReferenceId} amount={Amount} paidAt={PaidAt:o} " +
            "(outbox={OutboxId}). No downstream consumer wired yet.",
            fact.FeeId, fact.OrderId, fact.StudentId, fact.SourceModule,
            fact.SourceReferenceId, fact.Amount, fact.PaidAt, outboxMessageId);

        return Task.CompletedTask;
    }
}

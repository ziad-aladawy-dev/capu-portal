namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury.Events;

/// <summary>
/// Outbox fact published once per fee when an order settles. Consumers (e.g.
/// StudentServices) resolve their own state by <see cref="FeePaidFact.FeeId"/>
/// or <see cref="FeePaidFact.SourceReferenceId"/>. At-least-once delivery —
/// handlers must be idempotent.
/// </summary>
public static class FeePaidEvent
{
    public const string TypeKey = "payments.fee.paid";
}

public record FeePaidFact(
    System.Guid FeeId,
    System.Guid OrderId,
    System.Guid StudentId,
    string SourceModule,
    System.Guid? SourceReferenceId,
    decimal Amount,
    System.DateTime PaidAt);

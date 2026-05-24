namespace CapitalUniversity.Modules.Payments.Abstractions.Events;

/// <summary>
/// Producer-owned contract for the "an invoice has just been settled" outbox
/// fact. Lives in <c>Payments.Abstractions</c> so any downstream module can
/// reference the <see cref="TypeKey"/> + payload shape without taking a
/// dependency on the Payments implementation assembly.
///
/// <para>
/// Published by <c>PaymentVerificationService</c> the moment an invoice
/// transitions from <i>not paid</i> to <see cref="InvoiceStatus.Paid"/> (i.e.
/// once-only on the edge — partial payments do not fire this event, and a
/// duplicate "succeeded" transaction landing on an already-paid invoice is a
/// no-op). The outbox dispatcher then delivers the row at-least-once to every
/// registered handler under <see cref="TypeKey"/> — consumers must be
/// idempotent (the StudentServices handler catches the
/// <c>RequestNotWaitingPayment</c> conflict on replay).
/// </para>
/// </summary>
public static class InvoicePaidEvent
{
    /// <summary>
    /// Outbox discriminator. Matches the <c>{noun}.{verb}</c> convention used
    /// by the Schedule lifecycle events. Stable forever — renaming would
    /// silently break every downstream handler.
    /// </summary>
    public const string TypeKey = "payments.invoice.paid";
}

/// <summary>
/// Minimal payload — exactly the data downstream modules need to find the
/// originating obligation (typically by <see cref="InvoiceId"/>) and reflect
/// the payment back onto their own state. The Items/transactions detail is
/// intentionally omitted — a consumer that needs more can re-fetch the
/// invoice through <c>IInvoiceService</c>.
/// </summary>
public record InvoicePaidFact(
    System.Guid InvoiceId,
    System.Guid StudentId,
    decimal TotalAmount,
    string Currency,
    System.DateTime PaidAt);

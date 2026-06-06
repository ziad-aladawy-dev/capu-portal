using System.Runtime.CompilerServices;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Sync.Finance.Domain;

namespace CapitalUniversity.Sync.Finance.Sources;

/// <summary>
/// Deterministic in-memory simulator. Generates <see cref="TotalInvoices"/> rows
/// matching the field set of Core <c>Modules.Payments.Domain.Invoice</c>. Two
/// rows (#7 and #18) ship with non-positive totals so the validator drops them
/// and warning aggregation can be observed.
/// </summary>
public sealed class InMemoryExternalInvoiceSource : IExternalInvoiceSource
{
    public const int TotalInvoices = 40;

    private static readonly DateTimeOffset BaselineUpdatedAt =
        new DateTimeOffset(2026, 04, 01, 00, 00, 00, TimeSpan.Zero);

    private static readonly InvoiceStatus[] StatusCycle =
    {
        InvoiceStatus.Pending,
        InvoiceStatus.PartiallyPaid,
        InvoiceStatus.Paid,
        InvoiceStatus.Pending,
        InvoiceStatus.Cancelled
    };

    private readonly IReadOnlyList<ExternalInvoice> _store;

    public InMemoryExternalInvoiceSource()
    {
        var list = new List<ExternalInvoice>(TotalInvoices);
        for (var i = 1; i <= TotalInvoices; i++)
        {
            var hasInvalidTotal = i == 7 || i == 18;
            var status = StatusCycle[i % StatusCycle.Length];
            var studentNumber = ((i - 1) % 10) + 1;

            list.Add(new ExternalInvoice
            {
                ExternalInvoiceId = $"EXT-INV-{i:D6}",
                ExternalStudentId = $"EXT-S-{studentNumber:D4}",
                Status = status,
                TotalAmount = hasInvalidTotal ? 0m : Math.Round(500m + (i * 23.50m), 2),
                Currency = "EGP",
                DueAt = BaselineUpdatedAt.AddDays(i).UtcDateTime,
                ExternalUpdatedAt = BaselineUpdatedAt.AddMinutes(i),
                ExternalVersion = 1
            });
        }
        _store = list;
    }

    public async IAsyncEnumerable<ExternalInvoice> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var inv in _store)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sinceExclusive is null || inv.ExternalUpdatedAt > sinceExclusive.Value)
            {
                yield return inv;
                await Task.Yield();
            }
        }
    }
}

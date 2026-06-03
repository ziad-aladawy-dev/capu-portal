using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Sync.Abstractions.Contracts;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Finance.Pull;

/// <summary>
/// Sync.Finance writer — resolves each dispatch's <c>ExternalStudentId</c>
/// into the Core student's Guid via <see cref="ICoreWriteGateway.ResolveIdByExternalIdAsync"/>,
/// then hands the resolved <see cref="Invoice"/> list to the gateway for
/// upsert.
/// <para>
/// Invoices whose upstream student isn't in Core get skipped with a warning —
/// the gateway can't insert an Invoice without a valid <c>StudentId</c>.
/// </para>
/// </summary>
public sealed class InvoiceWriter : IRecordWriter<InvoiceSyncDispatch>
{
    private readonly ICoreWriteGateway _gateway;
    private readonly ILogger<InvoiceWriter> _logger;

    public InvoiceWriter(ICoreWriteGateway gateway, ILogger<InvoiceWriter> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<InvoiceSyncDispatch> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0) return 0;

        // Resolve FK per dispatch. A second pass to UpsertAsync only carries the
        // entities whose StudentId was resolvable.
        var resolved = new List<Invoice>(batch.Count);
        var unresolved = 0;
        foreach (var dispatch in batch)
        {
            var studentId = await _gateway
                .ResolveIdByExternalIdAsync<Student>(dispatch.ExternalStudentId, cancellationToken)
                .ConfigureAwait(false);

            if (studentId is null)
            {
                unresolved++;
                _logger.LogInformation(
                    "Sync.Finance: skipping Invoice ExternalId={ExternalInvoiceId} — its upstream student key {ExternalStudentId} has no matching Core student yet.",
                    dispatch.Entity.ExternallySourced.ExternalId, dispatch.ExternalStudentId);
                continue;
            }

            dispatch.Entity.StudentId = studentId.Value;
            resolved.Add(dispatch.Entity);
        }

        if (resolved.Count == 0)
        {
            return 0;
        }

        var result = await _gateway.UpsertAsync<Invoice>(
            resolved,
            applyUpdate: (existing, incoming) =>
            {
                // Re-binding StudentId is intentional — an invoice's owning student
                // can change upstream (mistakenly assigned, then corrected).
                existing.StudentId = incoming.StudentId;
                existing.Status = incoming.Status;
                existing.TotalAmount = incoming.TotalAmount;
                existing.Currency = incoming.Currency;
                existing.DueAt = incoming.DueAt;
            },
            new CoreUpsertOptions { AllowInsert = true, RespectExternalUpdatedAt = true },
            cancellationToken).ConfigureAwait(false);

        return result.Persisted;
    }
}

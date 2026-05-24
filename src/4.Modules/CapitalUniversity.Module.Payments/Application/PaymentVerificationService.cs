using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Modules.Payments.Abstractions.Events;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Payments.Domain;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;
using CapitalUniversity.Modules.Payments.Repositories;

namespace CapitalUniversity.Modules.Payments.Application;

/// <summary>
/// Records gateway-reported transactions and reflects the settled total
/// back onto the parent invoice's <see cref="InvoiceStatus"/>. Idempotency
/// is enforced at two layers:
///   <list type="number">
///     <item>Schema: unique index on <c>(InvoiceId, IdempotencyKey)</c>.</item>
///     <item>Service: <see cref="RecordAsync"/> looks up the existing row
///       first and returns it unchanged on a retry — never throws on dup.</item>
///   </list>
/// </summary>
public class PaymentVerificationService : IPaymentVerificationService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IValidator<RecordPaymentRequest> _validator;
    private readonly ICacheService _cache;
    private readonly IOutbox? _outbox;

    // SaveChanges is funneled through IInvoiceRepository.SaveTransactionWithIdempotencyAsync,
    // so IUnitOfWork isn't needed here — the repository persists directly on its
    // CoreDbContext and surfaces the idempotency-replay outcome to this service.
    //
    // <para>
    // <see cref="IOutbox"/> is optional so unit tests that don't wire the
    // outbox infrastructure still construct the service. In production it is
    // always registered, and the on-transition-to-Paid event is staged on the
    // same CoreDbContext as the invoice update so both commit atomically.
    // </para>
    public PaymentVerificationService(
        IInvoiceRepository invoices,
        IValidator<RecordPaymentRequest> validator,
        ICacheService cache,
        IOutbox? outbox = null)
    {
        _invoices = invoices;
        _validator = validator;
        _cache = cache;
        _outbox = outbox;
    }

    public async Task<PaymentTransactionResponse> RecordAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        // Fast-path idempotency probe. Cheap and correct: if the gateway
        // already sent this key, return the recorded outcome without touching
        // the invoice row at all.
        var existing = await _invoices.GetTransactionByKeyAsync(request.InvoiceId, request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return ToResponse(existing);
        }

        // H8 — recompute against fresh state on every attempt. The closure
        // reloads the invoice + transactions so ReflectSettledTotal sums the
        // post-conflict reality, not the pre-conflict snapshot. RowVersion
        // mismatch → DbUpdateConcurrencyException → ConcurrencyRetry triggers
        // a fresh attempt (with jittered backoff supplied by H10).
        var attempt = 0;
        return await ConcurrencyRetry.ExecuteAsync(async ct =>
        {
            attempt++;
            if (attempt > 1)
            {
                // Drop everything tracked from the previous failed attempt so
                // the next GetByIdAsync identity-resolves to a fresh entity
                // with the current RowVersion.
                _invoices.ResetChangeTracker();

                // Another writer may have inserted our idempotency row in the
                // window before we reload. Re-probe so we don't double-record.
                var replay = await _invoices.GetTransactionByKeyAsync(request.InvoiceId, request.IdempotencyKey, ct);
                if (replay is not null)
                {
                    return ToResponse(replay);
                }
            }

            var invoice = await _invoices.GetByIdAsync(request.InvoiceId, includeItems: false, includeTransactions: true, cancellationToken: ct)
                ?? throw new NotFoundException(LocalizedKeys.Payments.InvoiceNotFound);

            // Capture the pre-mutation status so we can detect the edge
            // transition into Paid below — only the transition fires the
            // outbox event, not every successful transaction landing on an
            // already-Paid invoice (would otherwise be a duplicate-delivery
            // hazard for downstream consumers).
            var wasAlreadyPaid = invoice.Status == InvoiceStatus.Paid;

            var tx = new PaymentTransaction
            {
                InvoiceId = invoice.Id,
                Provider = request.Provider,
                ProviderTransactionId = request.ProviderTransactionId,
                Status = request.Status,
                Amount = request.Amount,
                RawPayloadJson = string.IsNullOrEmpty(request.RawPayloadJson) ? "{}" : request.RawPayloadJson,
                IdempotencyKey = request.IdempotencyKey,
            };
            invoice.Transactions.Add(tx);

            if (request.Status == PaymentTransactionStatus.Succeeded)
            {
                ReflectSettledTotal(invoice);
            }
            invoice.UpdatedAt = DateTime.UtcNow;
            _invoices.Update(invoice);

            // Stage the lifecycle fact BEFORE SaveTransactionWithIdempotencyAsync
            // so the outbox row commits in the same transaction as the
            // invoice + transaction rows. If a duplicate idempotency key
            // causes SaveChangesAsync to throw, nothing is persisted — the
            // staged outbox row stays in the change tracker but is never
            // committed (no SaveChanges is called again in this attempt), and
            // ConcurrencyRetry's ResetChangeTracker on the next attempt
            // drops it before the new attempt re-enqueues. Net result:
            // exactly-on-edge delivery.
            var nowPaid = invoice.Status == InvoiceStatus.Paid;
            if (_outbox is not null && !wasAlreadyPaid && nowPaid)
            {
                await _outbox.EnqueueAsync(
                    InvoicePaidEvent.TypeKey,
                    new InvoicePaidFact(
                        invoice.Id,
                        invoice.StudentId,
                        invoice.TotalAmount,
                        invoice.Currency,
                        DateTime.UtcNow),
                    ct);
            }

            // P1.3 — narrow idempotency handling: the repo returns the existing
            // row on a unique-index collision (2627/2601 on
            // IX_PaymentTransactions_InvoiceId_IdempotencyKey). Any other
            // DbUpdateException still rethrows.
            var (savedTx, wasReplay) = await _invoices.SaveTransactionWithIdempotencyAsync(tx, ct);
            if (wasReplay)
            {
                return ToResponse(savedTx);
            }

            await _cache.RemoveAsync(InvoiceService.CacheKey(invoice.Id), ct);
            return ToResponse(savedTx);
        }, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentTransactionResponse>> GetForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var rows = await _invoices.GetTransactionsForInvoiceAsync(invoiceId, cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<PagedResult<PaymentTransactionResponse>> SearchAsync(PaymentTransactionSearchQuery query, CancellationToken cancellationToken = default)
    {
        // Scope checks: this service doesn't have IEffectiveScope today; the
        // route permission (PaymentTransactions.View) is the admin gate. If a
        // future caller wants per-student scope, the repository's StudentId
        // join makes it cheap to add an additional pre-check here.
        var page = await _invoices.SearchTransactionsAsync(query, cancellationToken);
        return new PagedResult<PaymentTransactionResponse>
        {
            Items = page.Items.Select(ToResponse).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    private static void ReflectSettledTotal(Invoice invoice)
    {
        // M9 — defence-in-depth: the global soft-delete query filter normally
        // hides IsDeleted rows on load, but a transaction soft-deleted after
        // we hydrated the collection (or one persisted into the in-memory
        // collection by a sibling write) would otherwise be counted here.
        // The extra predicate is cheap and keeps the settled total honest.
        var settled = invoice.Transactions
            .Where(t => !t.IsDeleted && t.Status == PaymentTransactionStatus.Succeeded)
            .Sum(t => t.Amount);

        if (settled >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
        }
        else if (settled > 0m)
        {
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }
    }

    private static PaymentTransactionResponse ToResponse(PaymentTransaction t) => new()
    {
        Id = t.Id,
        InvoiceId = t.InvoiceId,
        Provider = t.Provider,
        ProviderTransactionId = t.ProviderTransactionId,
        Status = t.Status,
        Amount = t.Amount,
        CreatedAt = t.CreatedAt,
    };
}

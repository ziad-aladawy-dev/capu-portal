using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Domain.Common;
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
    // Transaction collection caches (per-invoice list + search pages) keyed by a
    // version stamp; recording a payment rotates it and orphans them all.
    internal const string TxCollectionVersionKey = "payment-tx:coll:ver";
    internal const string InvoiceTxListKeyPrefix = "payment-tx:invoice:";
    internal const string TxSearchKeyPrefix = "payment-tx:search:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

    private readonly IInvoiceRepository _invoices;
    private readonly IValidator<RecordPaymentRequest> _validator;
    private readonly ICacheService _cache;
    private readonly IOutbox? _outbox;
    private readonly INotificationService? _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentVerificationService(
        IInvoiceRepository invoices,
        IValidator<RecordPaymentRequest> validator,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        IOutbox? outbox = null,
        INotificationService? notifications = null)
    {
        _invoices = invoices;
        _validator = validator;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
        _notifications = notifications;
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

            // Stage the lifecycle fact BEFORE SaveChangesAsync
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

                // Tell the student their payment landed. Staged on the same
                // outbox/DbContext as the InvoicePaidFact above, so it commits
                // atomically with the settlement in the SaveChangesAsync below —
                // and is dropped on the next ConcurrencyRetry attempt if this one
                // fails (exactly-on-edge, mirroring the fact enqueue). Bilingual
                // payload; the recipient sees it in their own culture.
                if (_notifications is not null)
                {
                    await _notifications.EnqueueNotificationAsync(
                        invoice.StudentId,
                        LocalizedJson.Of("تم استلام الدفعة", "Payment received"),
                        LocalizedJson.Of(
                            $"تم سداد فاتورتك بمبلغ {invoice.TotalAmount} {invoice.Currency} بنجاح.",
                            $"Your invoice of {invoice.TotalAmount} {invoice.Currency} has been paid successfully."),
                        NotificationType.Info,
                        ct);
                }
            }

            // P1.3 — narrow idempotency handling: we catch unique-index collision 
            // (2627/2601 on IX_PaymentTransactions_InvoiceId_IdempotencyKey).
            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                when (IsIdempotencyViolation(ex))
            {
                // Unique index collision — another thread won the race.
                // Reset tracker and fetch the winner's row.
                _invoices.ResetChangeTracker();
                var winner = await _invoices.GetTransactionByKeyAsync(request.InvoiceId, request.IdempotencyKey, ct);
                return ToResponse(winner!);
            }

            await _cache.RemoveAsync(InvoiceService.CacheKey(invoice.Id), ct);
            // The invoice's status (read model) changed → invalidate invoice
            // list/search caches; a new transaction → invalidate tx caches.
            await InvoiceService.BumpCollectionVersionAsync(_cache, ct);
            await BumpTxVersionAsync(ct);
            return ToResponse(tx);
        }, cancellationToken: cancellationToken);
    }

    private static bool IsIdempotencyViolation(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        if (ex.InnerException is not Microsoft.Data.SqlClient.SqlException sqlEx) return false;
        // 2627: Unique constraint violation. 2601: Unique index violation.
        return sqlEx.Number is 2627 or 2601;
    }

    public async Task<IReadOnlyList<PaymentTransactionResponse>> GetForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Stampede-protected read-through. Transactions carry no localizable
        // content, so the cached list is returned as-is (copied to avoid handing
        // out the cached reference). Invalidated by the tx version stamp.
        var version = await GetTxVersionAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<PaymentTransactionResponse>>(
            $"{InvoiceTxListKeyPrefix}{invoiceId:N}:{version}",
            async ct => (await _invoices.GetTransactionsForInvoiceAsync(invoiceId, ct)).Select(ToResponse).ToList(),
            CacheTtl,
            cancellationToken);
        return new List<PaymentTransactionResponse>(cached ?? new List<PaymentTransactionResponse>());
    }

    public async Task<PagedResult<PaymentTransactionResponse>> SearchAsync(PaymentTransactionSearchQuery query, CancellationToken cancellationToken = default)
    {
        // Scope checks: this service doesn't have IEffectiveScope today; the
        // route permission (PaymentTransactions.View) is the admin gate. If a
        // future caller wants per-student scope, the repository's StudentId
        // join makes it cheap to add an additional pre-check here.
        // Stampede-protected read-through keyed by the tx version + query signature.
        var version = await GetTxVersionAsync(cancellationToken);
        var page = await _cache.GetOrSetAsync<PagedResult<PaymentTransactionResponse>>(
            $"{TxSearchKeyPrefix}{version}:{BuildSearchSignature(query)}",
            async ct =>
            {
                var result = await _invoices.SearchTransactionsAsync(query, ct);
                return new PagedResult<PaymentTransactionResponse>
                {
                    Items = result.Items.Select(ToResponse).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                    TotalPages = result.TotalPages,
                };
            },
            CacheTtl,
            cancellationToken)
            ?? new PagedResult<PaymentTransactionResponse>
            {
                Items = new List<PaymentTransactionResponse>(),
                Page = query.NormalizedPage,
                PageSize = query.NormalizedPageSize,
            };

        return new PagedResult<PaymentTransactionResponse>
        {
            Items = new List<PaymentTransactionResponse>(page.Items),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    private async Task<string> GetTxVersionAsync(CancellationToken cancellationToken)
    {
        var v = await _cache.GetAsync<string>(TxCollectionVersionKey, cancellationToken);
        if (!string.IsNullOrEmpty(v)) return v;
        await _cache.SetAsync(TxCollectionVersionKey, "0", VersionTtl, cancellationToken);
        return "0";
    }

    private Task BumpTxVersionAsync(CancellationToken cancellationToken) =>
        _cache.SetAsync(TxCollectionVersionKey, Guid.NewGuid().ToString("N"), VersionTtl, cancellationToken);

    private static string BuildSearchSignature(PaymentTransactionSearchQuery q) =>
        string.Join('|',
            $"inv={q.InvoiceId?.ToString("N") ?? string.Empty}",
            $"stu={q.StudentId?.ToString("N") ?? string.Empty}",
            $"status={(q.Status.HasValue ? ((int)q.Status.Value).ToString() : string.Empty)}",
            $"prov={q.Provider ?? string.Empty}",
            $"from={q.From?.ToString("O") ?? string.Empty}",
            $"to={q.To?.ToString("O") ?? string.Empty}",
            $"min={q.MinAmount?.ToString() ?? string.Empty}",
            $"max={q.MaxAmount?.ToString() ?? string.Empty}",
            $"q={q.Search ?? string.Empty}",
            $"sort={q.Sort ?? string.Empty}",
            $"p={q.NormalizedPage}",
            $"ps={q.NormalizedPageSize}");

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

using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Modules.Payments.Application;
using CapitalUniversity.Modules.Payments.Application.Validators;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Payments.Repositories;
using FluentAssertions;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.Payments;

/// <summary>
/// Settlement edge cases on <see cref="PaymentVerificationService"/>.
/// Complements the existing <c>PaymentVerificationServiceTests</c> by pinning
/// the financial-correctness contracts that the audit flagged as silently
/// uncovered: overpayment behaviour, soft-deleted exclusion, multi-transaction
/// accumulation, edge-triggered outbox delivery, and the validator's negative
/// surface (zero / negative amount, missing idempotency key, etc.).
///
/// <para>
/// Mocks reuse the same <c>ConcurrencyRetry</c>-friendly pattern as the
/// existing tests: <see cref="IInvoiceRepository.GetTransactionByKeyAsync"/>
/// returns null (no replay), <see cref="IInvoiceRepository.GetByIdAsync"/>
/// returns the in-memory invoice the test set up, and
/// <see cref="IUnitOfWork.SaveChangesAsync"/> succeeds. The service's
/// <see cref="PaymentVerificationService.ReflectSettledTotal"/> branches are
/// exercised through the live invoice instance the test inspects after the
/// call returns.
/// </para>
/// </summary>
public class PaymentSettlementCoverageTests
{
    private sealed class StubCache : ICacheService
    {
        public int RemoveCalls;
        public Task<T?> GetAsync<T>(string key, CancellationToken c = default) => Task.FromResult(default(T?));
        public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken c = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken c = default) { RemoveCalls++; return Task.CompletedTask; }
    }

    private sealed class RecordingOutbox : IOutbox
    {
        public List<(string Type, object Payload)> Messages { get; } = new();
        public Task EnqueueAsync<TPayload>(string messageType, TPayload payload, CancellationToken cancellationToken = default)
        {
            Messages.Add((messageType, payload!));
            return Task.CompletedTask;
        }
    }

    private static (PaymentVerificationService Service, Mock<IInvoiceRepository> Repo, Mock<IUnitOfWork> Uow, StubCache Cache, RecordingOutbox Outbox) Build()
    {
        var repo = new Mock<IInvoiceRepository>();
        var uow = new Mock<IUnitOfWork>();
        var cache = new StubCache();
        var outbox = new RecordingOutbox();
        return (
            new PaymentVerificationService(repo.Object, new RecordPaymentValidator(), cache, uow.Object, outbox),
            repo, uow, cache, outbox);
    }

    private sealed class RecordingNotifications : CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.INotificationService
    {
        public List<(Guid Recipient, string Title, string Message, CapitalUniversity.Core.Domain.Common.NotificationType Type)> Enqueued { get; } = new();

        public Task EnqueueNotificationAsync(Guid recipientUserId, string title, string message, CapitalUniversity.Core.Domain.Common.NotificationType type, CancellationToken cancellationToken = default)
        {
            Enqueued.Add((recipientUserId, title, message, type));
            return Task.CompletedTask;
        }

        // Unused by these tests — the service only ever calls EnqueueNotificationAsync.
        public Task CreateNotificationAsync(Guid recipientUserId, string title, string message, CapitalUniversity.Core.Domain.Common.NotificationType type, Guid? idempotencyKey = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs.NotificationDto>> GetUserNotificationsAsync(Guid userId) => Task.FromResult(Enumerable.Empty<CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs.NotificationDto>());
        public Task<IEnumerable<CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs.NotificationDto>> GetUnreadNotificationsAsync(Guid userId) => Task.FromResult(Enumerable.Empty<CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs.NotificationDto>());
        public Task MarkAsReadAsync(Guid notificationId, Guid userId) => Task.CompletedTask;
        public Task<int> MarkManyAsReadAsync(IReadOnlyList<Guid> notificationIds, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<CapitalUniversity.Core.Abstractions.Shared.PagedResult<CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs.NotificationDto>> SearchAsync(Guid userId, CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs.NotificationSearchQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new CapitalUniversity.Core.Abstractions.Shared.PagedResult<CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs.NotificationDto>());
    }

    private static (PaymentVerificationService Service, Mock<IInvoiceRepository> Repo, RecordingNotifications Notifications) BuildWithNotifications()
    {
        var repo = new Mock<IInvoiceRepository>();
        var uow = new Mock<IUnitOfWork>();
        var cache = new StubCache();
        var outbox = new RecordingOutbox();
        var notifications = new RecordingNotifications();
        return (
            new PaymentVerificationService(repo.Object, new RecordPaymentValidator(), cache, uow.Object, outbox, notifications),
            repo, notifications);
    }

    private static RecordPaymentRequest ValidRequest(
        Guid invoiceId,
        decimal amount,
        PaymentTransactionStatus status = PaymentTransactionStatus.Succeeded,
        string? idem = null) => new()
        {
            InvoiceId = invoiceId,
            Provider = "Stripe",
            ProviderTransactionId = "pi_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Status = status,
            Amount = amount,
            IdempotencyKey = idem ?? Guid.NewGuid().ToString("N"),
            RawPayloadJson = "{\"ok\":true}",
        };

    private static Invoice PendingInvoice(decimal total, params PaymentTransaction[] preExisting)
    {
        var inv = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            Status = InvoiceStatus.Pending,
            TotalAmount = total,
            Currency = "EGP",
        };
        foreach (var t in preExisting) inv.Transactions.Add(t);
        return inv;
    }

    private static void WireRepo(Mock<IInvoiceRepository> repo, Invoice inv)
    {
        repo.Setup(r => r.GetTransactionByKeyAsync(inv.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repo.Setup(r => r.GetByIdAsync(inv.Id, false, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
    }

    // ── Overpayment (audit finding C-6) ──────────────────────────────────────

    [Fact]
    public async Task Record_AmountStrictlyGreaterThanTotal_FlipsToPaid_NoOverpaidState()
    {
        // Audit finding C-6: settlement uses `settled >= TotalAmount` and there
        // is no Overpaid state in the InvoiceStatus enum. A single payment for
        // more than the invoice total flips the invoice to Paid with no
        // explicit signal that the customer paid in excess. This test pins the
        // current behaviour so a future fix that introduces refund / overpaid
        // semantics has to update this assertion deliberately.
        var (sut, repo, _, _, _) = Build();
        var invoice = PendingInvoice(total: 100m);
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 150m));

        invoice.Status.Should().Be(InvoiceStatus.Paid,
            "documented gap: over-payments silently flip to Paid — the audit C-6 finding lives here");
        invoice.Transactions.Should().HaveCount(1);
        invoice.Transactions.Single().Amount.Should().Be(150m,
            "the recorded amount is preserved even though it exceeds the invoice total");
    }

    [Fact]
    public async Task Record_AmountExactlyEqualToTotal_FlipsToPaid()
    {
        // Boundary: `settled >= TotalAmount` must include equality. A penny-
        // exact payment is the most common path and must not leave the invoice
        // PartiallyPaid.
        var (sut, repo, _, _, _) = Build();
        var invoice = PendingInvoice(total: 100m);
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 100m));

        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Record_AccumulatedSucceededTransactionsCrossingTotal_FlipsToPaid()
    {
        // Multi-payment plan: two installments each less than the total but
        // summing to (or past) it must result in Paid. The settlement reads
        // every Succeeded tx attached to the invoice — this test covers the
        // accumulation path that the single-payment test in
        // PaymentVerificationServiceTests doesn't exercise.
        var (sut, repo, _, _, _) = Build();
        var alreadyPaid = new PaymentTransaction
        {
            InvoiceId = Guid.Empty, // populated when Invoice attaches it below
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 40m,
            IdempotencyKey = "first",
        };
        var invoice = PendingInvoice(total: 100m, alreadyPaid);
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 60m, idem: "second"));

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Record_PendingTransactionDoesNotContribute_StaysPending()
    {
        // ReflectSettledTotal filters by Status == Succeeded. A Pending
        // transaction sitting in the collection (gateway still verifying)
        // must not move the invoice's status. This is the failure mode where
        // a webhook fires too eagerly.
        var (sut, repo, _, _, _) = Build();
        var pending = new PaymentTransaction
        {
            Status = PaymentTransactionStatus.Pending,
            Amount = 100m,
            IdempotencyKey = "pending-tx",
        };
        var invoice = PendingInvoice(total: 100m, pending);
        WireRepo(repo, invoice);

        // Record a Failed transaction — neither contributes to the settled
        // sum, so the invoice must stay Pending.
        await sut.RecordAsync(ValidRequest(invoice.Id, 50m, status: PaymentTransactionStatus.Failed));

        invoice.Status.Should().Be(InvoiceStatus.Pending,
            "neither pending nor failed transactions contribute to the settled total");
    }

    [Fact]
    public async Task Record_SoftDeletedTransactionExcludedFromSum_StaysPending()
    {
        // M9 defence-in-depth: even though the global IsDeleted query filter
        // hides soft-deleted rows on load, a tx soft-deleted after hydration
        // would otherwise be counted. The service applies an explicit
        // `!t.IsDeleted` predicate (PaymentVerificationService.cs:205) — this
        // test pins that defence.
        var (sut, repo, _, _, _) = Build();
        var deleted = new PaymentTransaction
        {
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 100m,
            IdempotencyKey = "deleted-tx",
            IsDeleted = true,
        };
        var invoice = PendingInvoice(total: 100m, deleted);
        WireRepo(repo, invoice);

        // Adding a Failed transaction: the soft-deleted Succeeded shouldn't
        // mask the failure. Invoice must stay Pending — neither contributes.
        await sut.RecordAsync(ValidRequest(invoice.Id, 100m, status: PaymentTransactionStatus.Failed));

        invoice.Status.Should().Be(InvoiceStatus.Pending,
            "soft-deleted succeeded transaction must not contribute to the settled total");
    }

    [Fact]
    public async Task Record_SucceededAtZeroAmount_LeavesInvoicePending()
    {
        // Zero-amount payments pass validation (Amount >= 0) but settle no
        // money. The invoice must stay Pending — flipping to PartiallyPaid
        // on a zero contribution would lie about the customer's progress
        // (ReflectSettledTotal: `else if (settled > 0m)`).
        var (sut, repo, _, _, _) = Build();
        var invoice = PendingInvoice(total: 100m);
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 0m));

        invoice.Status.Should().Be(InvoiceStatus.Pending,
            "a zero-amount succeeded transaction settles nothing — status must not change");
    }

    // ── Outbox edge-triggered delivery ───────────────────────────────────────

    [Fact]
    public async Task Record_TransitionsPendingToPaid_EnqueuesInvoicePaidEventExactlyOnce()
    {
        // Exactly-on-edge delivery: the outbox row commits together with the
        // invoice + transaction rows so a downstream consumer (e.g. ledger,
        // notification) sees the Paid fact exactly when the DB says Paid.
        var (sut, repo, _, _, outbox) = Build();
        var invoice = PendingInvoice(total: 100m);
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 100m));

        outbox.Messages.Should().HaveCount(1);
        outbox.Messages.Single().Type.Should().Be("payments.invoice.paid",
            "the outbox event type (InvoicePaidEvent.TypeKey) is the contract downstream consumers subscribe to — pinning it catches a silent rename");
    }

    [Fact]
    public async Task Record_AlreadyPaidInvoice_DoesNotReEnqueueOutboxEvent()
    {
        // The wasAlreadyPaid guard in PaymentVerificationService.cs:102 stops
        // duplicate delivery when a transaction lands on an already-Paid
        // invoice (think: the gateway sends a duplicate Succeeded webhook
        // after the invoice was settled by a sibling). Without this guard a
        // downstream consumer (audit ledger, notifications) would see the
        // invoice.paid event twice.
        var (sut, repo, _, _, outbox) = Build();
        var existingPayment = new PaymentTransaction
        {
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 100m,
            IdempotencyKey = "first",
        };
        var invoice = PendingInvoice(total: 100m, existingPayment);
        invoice.Status = InvoiceStatus.Paid; // already settled before this call
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 50m, idem: "second"));

        outbox.Messages.Should().BeEmpty(
            "edge-triggered: the event fires on the Pending→Paid transition, not on every successful tx");
    }

    [Fact]
    public async Task Record_PartialPayment_DoesNotEnqueueOutbox()
    {
        // PartiallyPaid is a non-final state and must not trigger the
        // downstream Paid event. The outbox carries lifecycle facts, not
        // progress reports.
        var (sut, repo, _, _, outbox) = Build();
        var invoice = PendingInvoice(total: 100m);
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 50m));

        invoice.Status.Should().Be(InvoiceStatus.PartiallyPaid);
        outbox.Messages.Should().BeEmpty();
    }

    // ── Student payment-success notification ─────────────────────────────────

    [Fact]
    public async Task Record_TransitionsPendingToPaid_NotifiesStudentOnce_WithAmountAndCurrency()
    {
        // On the Pending→Paid edge the student is told their payment landed.
        // Staged on the same outbox/DbContext as the InvoicePaidFact, so it
        // commits atomically with settlement. The message is bilingual JSON;
        // it must carry the amount and currency the student actually paid.
        var (sut, repo, notifications) = BuildWithNotifications();
        var invoice = PendingInvoice(total: 100m);
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 100m));

        notifications.Enqueued.Should().HaveCount(1, "exactly one notification fires on the settlement edge");
        var sent = notifications.Enqueued.Single();
        sent.Recipient.Should().Be(invoice.StudentId, "the owning student is the recipient");
        sent.Type.Should().Be(CapitalUniversity.Core.Domain.Common.NotificationType.Info);
        sent.Message.Should().Contain("100").And.Contain("EGP",
            "the localized payload tells the student how much settled and in which currency");
        sent.Title.Should().Contain("\"ar\"").And.Contain("\"en\"",
            "title is a bilingual LocalizedJson payload, not a bare string");
    }

    [Fact]
    public async Task Record_AlreadyPaidInvoice_DoesNotNotifyStudentAgain()
    {
        // Same edge-trigger guard as the outbox event: a duplicate Succeeded
        // webhook landing on an already-Paid invoice must not re-notify the
        // student.
        var (sut, repo, notifications) = BuildWithNotifications();
        var existingPayment = new PaymentTransaction
        {
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 100m,
            IdempotencyKey = "first",
        };
        var invoice = PendingInvoice(total: 100m, existingPayment);
        invoice.Status = InvoiceStatus.Paid; // already settled before this call
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 50m, idem: "second"));

        notifications.Enqueued.Should().BeEmpty(
            "edge-triggered: the student is notified on the Pending→Paid transition, not on every successful tx");
    }

    // ── Cache invalidation ───────────────────────────────────────────────────

    [Fact]
    public async Task Record_OnSuccessfulRecord_InvalidatesInvoiceCacheEntry()
    {
        // InvoiceService caches the response payload per id; a settlement
        // change makes the cached version stale. The cache.RemoveAsync call
        // is the single invalidation point — this test pins it so a future
        // refactor that moves the call doesn't silently leave stale invoice
        // payloads serving.
        var (sut, repo, _, cache, _) = Build();
        var invoice = PendingInvoice(total: 100m);
        WireRepo(repo, invoice);

        await sut.RecordAsync(ValidRequest(invoice.Id, 50m));

        cache.RemoveCalls.Should().Be(1);
    }

    // ── Negative test cases via RecordPaymentValidator ───────────────────────
    //
    // The validator is the gate that protects the service from malformed
    // requests. These tests pin its current contract so a future relax
    // (or tighten) is a deliberate, reviewed change.

    [Fact]
    public async Task Record_EmptyInvoiceId_ThrowsValidation()
    {
        var (sut, _, _, _, _) = Build();
        var req = ValidRequest(Guid.NewGuid(), 100m);
        req.InvoiceId = Guid.Empty;

        await sut.Invoking(s => s.RecordAsync(req))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Record_EmptyIdempotencyKey_ThrowsValidation()
    {
        // The idempotency key is the dedup primary key for retried gateway
        // webhooks — an empty value collapses every payment for the same
        // invoice onto a single row. The validator must reject it.
        var (sut, _, _, _, _) = Build();
        var req = ValidRequest(Guid.NewGuid(), 100m);
        req.IdempotencyKey = string.Empty;

        await sut.Invoking(s => s.RecordAsync(req))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Record_EmptyProvider_ThrowsValidation()
    {
        var (sut, _, _, _, _) = Build();
        var req = ValidRequest(Guid.NewGuid(), 100m);
        req.Provider = string.Empty;

        await sut.Invoking(s => s.RecordAsync(req))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Record_EmptyProviderTransactionId_ThrowsValidation()
    {
        var (sut, _, _, _, _) = Build();
        var req = ValidRequest(Guid.NewGuid(), 100m);
        req.ProviderTransactionId = string.Empty;

        await sut.Invoking(s => s.RecordAsync(req))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Record_NegativeAmount_ThrowsValidation()
    {
        // Negative payments represent refunds and must not flow through
        // RecordAsync — they need a separate Refund flow. Until that flow
        // exists, the validator's Amount >= 0 rule is the gate.
        var (sut, _, _, _, _) = Build();
        var req = ValidRequest(Guid.NewGuid(), -1m);

        await sut.Invoking(s => s.RecordAsync(req))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Record_OverlongIdempotencyKey_ThrowsValidation()
    {
        // The 128-char ceiling matches the DB column width. Without this
        // guard, a runaway provider key would surface as a SQL truncation
        // error from deep in EF — the validator surface is the right
        // boundary to fail.
        var (sut, _, _, _, _) = Build();
        var req = ValidRequest(Guid.NewGuid(), 100m);
        req.IdempotencyKey = new string('k', 129);

        await sut.Invoking(s => s.RecordAsync(req))
            .Should().ThrowAsync<ValidationException>();
    }

    // ── Negative test cases via CreateInvoiceValidator ───────────────────────

    [Fact]
    public void CreateInvoice_NegativeItemAmount_FailsValidation()
    {
        var validator = new CreateInvoiceValidator();
        var req = new CreateInvoiceRequest
        {
            StudentId = Guid.NewGuid(),
            Currency = "EGP",
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Amount = -1m, FeeType = "Tuition", SourceModule = "registration", Description = "x" },
            },
        };

        var result = validator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Amount"));
    }

    [Fact]
    public void CreateInvoice_EmptyStudentId_FailsValidation()
    {
        var validator = new CreateInvoiceValidator();
        var req = new CreateInvoiceRequest
        {
            StudentId = Guid.Empty,
            Currency = "EGP",
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Amount = 10m, FeeType = "Tuition", SourceModule = "registration" },
            },
        };

        validator.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateInvoice_CurrencyWrongLength_FailsValidation()
    {
        // ISO-4217 codes are exactly 3 chars. Anything else is either a
        // typo or an attempt to pass a name through.
        var validator = new CreateInvoiceValidator();
        var req = new CreateInvoiceRequest
        {
            StudentId = Guid.NewGuid(),
            Currency = "EGYPT",
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Amount = 10m, FeeType = "Tuition", SourceModule = "registration" },
            },
        };

        validator.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateInvoice_EmptyItems_FailsValidation()
    {
        var validator = new CreateInvoiceValidator();
        var req = new CreateInvoiceRequest
        {
            StudentId = Guid.NewGuid(),
            Currency = "EGP",
            Items = new List<CreateInvoiceItemRequest>(),
        };

        var result = validator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Items"));
    }

    [Fact]
    public void CreateInvoice_ItemMissingFeeType_FailsValidation()
    {
        var validator = new CreateInvoiceValidator();
        var req = new CreateInvoiceRequest
        {
            StudentId = Guid.NewGuid(),
            Currency = "EGP",
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Amount = 10m, FeeType = string.Empty, SourceModule = "registration" },
            },
        };

        validator.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateInvoice_ItemZeroAmount_PassesValidation()
    {
        // Zero amounts are permitted by Amount >= 0 — used for scholarship-
        // covered terms. This test explicitly pins the current contract so
        // a future tighter rule (Amount > 0) is a conscious change.
        var validator = new CreateInvoiceValidator();
        var req = new CreateInvoiceRequest
        {
            StudentId = Guid.NewGuid(),
            Currency = "EGP",
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Amount = 0m, FeeType = "Scholarship", SourceModule = "academics" },
            },
        };

        validator.Validate(req).IsValid.Should().BeTrue();
    }
}

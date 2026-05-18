using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Payments.DTOs;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Application.Payments;
using CapitalUniversity.Core.Application.Payments.Validators;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Payments;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Payments;

/// <summary>
/// Payment verification contract: idempotency, status reflection
/// (Pending → PartiallyPaid → Paid), cache invalidation on every successful
/// record.
/// </summary>
public class PaymentVerificationServiceTests
{
    private sealed class StubCache : ICacheService
    {
        public int RemoveCalls;
        public Task<T?> GetAsync<T>(string key, CancellationToken c = default) => Task.FromResult(default(T?));
        public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken c = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken c = default) { RemoveCalls++; return Task.CompletedTask; }
    }

    private static (PaymentVerificationService Service, Mock<IInvoiceRepository> Repo, Mock<IUnitOfWork> Uow, StubCache Cache) Build()
    {
        var repo = new Mock<IInvoiceRepository>();
        var uow = new Mock<IUnitOfWork>();
        var cache = new StubCache();
        return (new PaymentVerificationService(uow.Object, repo.Object, new RecordPaymentValidator(), cache), repo, uow, cache);
    }

    private static RecordPaymentRequest ValidRequest(Guid invoiceId, decimal amount, PaymentTransactionStatus status = PaymentTransactionStatus.Succeeded, string? idem = null) => new()
    {
        InvoiceId = invoiceId,
        Provider = "Stripe",
        ProviderTransactionId = "pi_" + Guid.NewGuid().ToString("N").Substring(0, 8),
        Status = status,
        Amount = amount,
        IdempotencyKey = idem ?? Guid.NewGuid().ToString("N"),
        RawPayloadJson = "{\"ok\":true}",
    };

    [Fact]
    public async Task Record_Replay_ReturnsExistingTransactionWithoutRecording()
    {
        var (sut, repo, uow, _) = Build();
        var invoiceId = Guid.NewGuid();
        var existing = new PaymentTransaction
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceId, Provider = "Stripe",
            ProviderTransactionId = "px", Status = PaymentTransactionStatus.Succeeded, Amount = 100m,
            IdempotencyKey = "key-1",
        };
        repo.Setup(r => r.GetTransactionByKeyAsync(invoiceId, "key-1", default)).ReturnsAsync(existing);

        var request = ValidRequest(invoiceId, 100m, idem: "key-1");
        var response = await sut.RecordAsync(request);

        response.Id.Should().Be(existing.Id);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Never, "replays must not mutate state");
    }

    [Fact]
    public async Task Record_SucceededAtFullAmount_TransitionsInvoiceToPaid()
    {
        var (sut, repo, _, cache) = Build();
        var invoiceId = Guid.NewGuid();
        var inv = new Invoice { Id = invoiceId, StudentId = Guid.NewGuid(), Status = InvoiceStatus.Pending, TotalAmount = 100m };
        repo.Setup(r => r.GetTransactionByKeyAsync(invoiceId, It.IsAny<string>(), default)).ReturnsAsync((PaymentTransaction?)null);
        repo.Setup(r => r.GetByIdAsync(invoiceId, false, true, default)).ReturnsAsync(inv);

        var response = await sut.RecordAsync(ValidRequest(invoiceId, 100m));

        response.Status.Should().Be(PaymentTransactionStatus.Succeeded);
        inv.Status.Should().Be(InvoiceStatus.Paid);
        cache.RemoveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Record_PartialAmount_TransitionsInvoiceToPartiallyPaid()
    {
        var (sut, repo, _, _) = Build();
        var invoiceId = Guid.NewGuid();
        var inv = new Invoice { Id = invoiceId, StudentId = Guid.NewGuid(), Status = InvoiceStatus.Pending, TotalAmount = 100m };
        repo.Setup(r => r.GetTransactionByKeyAsync(invoiceId, It.IsAny<string>(), default)).ReturnsAsync((PaymentTransaction?)null);
        repo.Setup(r => r.GetByIdAsync(invoiceId, false, true, default)).ReturnsAsync(inv);

        await sut.RecordAsync(ValidRequest(invoiceId, 40m));

        inv.Status.Should().Be(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task Record_FailedTransaction_DoesNotChangeInvoiceStatus()
    {
        var (sut, repo, _, _) = Build();
        var invoiceId = Guid.NewGuid();
        var inv = new Invoice { Id = invoiceId, StudentId = Guid.NewGuid(), Status = InvoiceStatus.Pending, TotalAmount = 100m };
        repo.Setup(r => r.GetTransactionByKeyAsync(invoiceId, It.IsAny<string>(), default)).ReturnsAsync((PaymentTransaction?)null);
        repo.Setup(r => r.GetByIdAsync(invoiceId, false, true, default)).ReturnsAsync(inv);

        await sut.RecordAsync(ValidRequest(invoiceId, 100m, status: PaymentTransactionStatus.Failed));

        inv.Status.Should().Be(InvoiceStatus.Pending);
    }

    [Fact]
    public async Task Record_UnknownInvoice_ThrowsNotFound()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetTransactionByKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), default)).ReturnsAsync((PaymentTransaction?)null);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), false, true, default)).ReturnsAsync((Invoice?)null);

        await sut.Invoking(s => s.RecordAsync(ValidRequest(Guid.NewGuid(), 100m)))
            .Should().ThrowAsync<NotFoundException>();
    }
}

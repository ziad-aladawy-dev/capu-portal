using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Modules.Payments.Application;
using CapitalUniversity.Modules.Payments.Application.Validators;
using CapitalUniversity.Modules.Payments.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Payments.Domain;
using FluentAssertions;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.Payments;

/// <summary>
/// Invoice service contract: validates, totals items, transitions status
/// only through guarded paths, and invalidates the shared cache entry on
/// mutation.
/// </summary>
public class InvoiceServiceTests
{
    private sealed class StubCache : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();
        public int RemoveCalls;
        public Task<T?> GetAsync<T>(string key, CancellationToken c = default) =>
            Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);
        public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken c = default)
        { _store[key] = value; return Task.CompletedTask; }
        public Task RemoveAsync(string key, CancellationToken c = default)
        { RemoveCalls++; _store.Remove(key); return Task.CompletedTask; }
    }

    private static (InvoiceService Service, Mock<IInvoiceRepository> Repo, Mock<IUnitOfWork> Uow, StubCache Cache) Build()
    {
        var repo = new Mock<IInvoiceRepository>();
        var uow = new Mock<IUnitOfWork>();
        var cache = new StubCache();
        var scope = new Mock<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.IEffectiveScope>();
        // Default: every student is in-scope so existing service tests keep
        // their pre-P1.1 semantics.
        scope.Setup(s => s.CanAccessStudentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        scope.Setup(s => s.CanAccessStructureNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return (new InvoiceService(uow.Object, repo.Object, new CreateInvoiceValidator(), cache, scope.Object), repo, uow, cache);
    }

    private static CreateInvoiceRequest ValidRequest(Guid? studentId = null) => new()
    {
        StudentId = studentId ?? Guid.NewGuid(),
        Currency = "EGP",
        Items = new List<CreateInvoiceItemRequest>
        {
            new() { Amount = 100m, FeeType = "Tuition", SourceModule = "registration", Description = "Sem 1" },
            new() { Amount = 50m,  FeeType = "LabFee",  SourceModule = "registration", Description = "Lab" },
        },
    };

    [Fact]
    public async Task Create_SumsItemsIntoTotal_AndPersists()
    {
        var (sut, repo, uow, _) = Build();
        Invoice? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Invoice>(), default))
            .Callback<Invoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var id = await sut.CreateAsync(ValidRequest());

        id.Should().NotBeEmpty();
        captured.Should().NotBeNull();
        captured!.TotalAmount.Should().Be(150m);
        captured.Items.Should().HaveCount(2);
        captured.Status.Should().Be(InvoiceStatus.Pending);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Create_NoItems_ThrowsValidation()
    {
        var (sut, _, _, _) = Build();
        var req = ValidRequest();
        req.Items.Clear();
        await sut.Invoking(s => s.CreateAsync(req)).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetById_CacheMissThenHit_HitsRepoOnce()
    {
        var (sut, repo, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(id, true, false, default)).ReturnsAsync(new Invoice
        {
            Id = id, StudentId = Guid.NewGuid(), Status = InvoiceStatus.Pending, TotalAmount = 100m, Currency = "EGP",
        });

        await sut.GetByIdAsync(id);
        await sut.GetByIdAsync(id);

        repo.Verify(r => r.GetByIdAsync(id, true, false, default), Times.Once);
    }

    [Fact]
    public async Task Cancel_PaidInvoice_ThrowsConflict()
    {
        var (sut, repo, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(id, false, true, default)).ReturnsAsync(new Invoice
        {
            Id = id, StudentId = Guid.NewGuid(), Status = InvoiceStatus.Paid, TotalAmount = 100m,
        });

        await sut.Invoking(s => s.CancelAsync(id, new CancelInvoiceRequest { Reason = "test" }))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Cancel_HappyPath_TransitionsAndInvalidatesCache()
    {
        var (sut, repo, _, cache) = Build();
        var id = Guid.NewGuid();
        var inv = new Invoice { Id = id, StudentId = Guid.NewGuid(), Status = InvoiceStatus.Pending, TotalAmount = 100m };
        repo.Setup(r => r.GetByIdAsync(id, false, true, default)).ReturnsAsync(inv);

        await sut.CancelAsync(id, new CancelInvoiceRequest { Reason = "test" });

        inv.Status.Should().Be(InvoiceStatus.Cancelled);
        cache.RemoveCalls.Should().Be(1);
    }
}

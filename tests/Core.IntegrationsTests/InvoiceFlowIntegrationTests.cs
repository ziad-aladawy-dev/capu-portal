using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.IntegrationsTests._Helpers;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Modules.Payments.Application;
using CapitalUniversity.Modules.Payments.Application.Validators;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Payments.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
// InvoiceRepository's CLR namespace is CapitalUniversity.Core.Infrastructure.Repositories
// (legacy from when the class lived in Core — audit L-7 tracks the rename).
using InvoiceRepository = CapitalUniversity.Core.Infrastructure.Repositories.InvoiceRepository;

namespace CapitalUniversity.Core.IntegrationsTests.Payments;

/// <summary>
/// DB-level coverage for the invoice creation path. Closes audit P1-7
/// (integration tests for the highest-risk subsystem) by exercising the
/// real <see cref="InvoiceService"/> + <see cref="InvoiceRepository"/>
/// stack against an InMemory <see cref="CoreDbContext"/>.
///
/// <para>
/// Scope is deliberately the <b>creation half</b> of the financial flow:
/// the InvoiceService.CreateAsync path (validate → scope-check → AddAsync →
/// SaveChanges) is fully exercised, including item-sum totals, persistence,
/// and the repository-layer round-trip. Payment settlement involves the
/// service's <c>Update</c> + <c>ConcurrencyRetry</c> path which depends on
/// SQL Server <c>rowversion</c> semantics the InMemory provider does not
/// emulate faithfully — that surface is tested via the unit-level
/// <c>PaymentSettlementCoverageTests</c> in <c>Core.UniTests</c>, which
/// uses the same service with a mock repository.
/// </para>
/// </summary>
public class InvoiceFlowIntegrationTests : IDisposable
{
    private readonly CoreDbContext _db;
    private readonly InvoiceRepository _repo;
    private readonly InvoiceService _service;
    private readonly StubCache _cache;

    public InvoiceFlowIntegrationTests()
    {
        // Module-owned EF configurations (Invoice / InvoiceItem / PaymentTransaction)
        // are picked up by CoreDbContext through this static list — see
        // CoreDbContext.cs:154 and the Sync.Host bootstrap. Adding the
        // Module.Payments assembly here so the InMemory model knows about
        // the Invoice DbSet.
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(typeof(Invoice).Assembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(typeof(Invoice).Assembly);
        }

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: "InvoiceFlow_" + Guid.NewGuid())
            .Options;
        _db = new CoreDbContext(options);
        _repo = new InvoiceRepository(_db);
        _cache = new StubCache();

        var scope = new Mock<IEffectiveScope>();
        scope.Setup(s => s.CanAccessStudentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        _service = new InvoiceService(
            new DbContextUnitOfWork(_db),
            _repo,
            new CreateInvoiceValidator(),
            _cache,
            scope.Object,
            new TestLocalizationService());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_PersistsInvoiceWithItems_AndComputesTotal()
    {
        // Happy path: service validates, scope-checks, totals items, persists.
        // The DB-level round-trip catches issues a mocked repository would not:
        // EF cascade-saving Items as part of the same SaveChanges, decimal
        // precision on the Amount column, and the (StudentId, Status) index
        // not rejecting the row.
        var studentId = Guid.NewGuid();

        var invoiceId = await _service.CreateAsync(new CreateInvoiceRequest
        {
            StudentId = studentId,
            Currency = "EGP",
            Items =
            {
                new CreateInvoiceItemRequest { Amount = 100m, FeeType = "Tuition", SourceModule = "registration", Description = "Sem 1" },
                new CreateInvoiceItemRequest { Amount = 50m,  FeeType = "LabFee",  SourceModule = "registration", Description = "Lab" },
            },
        });

        // Re-fetch from the DB to prove the row landed durably (not just the
        // service's in-memory work).
        var stored = await _db.Set<Invoice>()
            .Include(i => i.Items)
            .SingleAsync(i => i.Id == invoiceId);

        stored.StudentId.Should().Be(studentId);
        stored.Status.Should().Be(InvoiceStatus.Pending);
        stored.Currency.Should().Be("EGP");
        stored.TotalAmount.Should().Be(150m,
            "Invoice.TotalAmount must equal sum(Items.Amount) at persist time (Invoice.cs:14-17)");
        stored.Items.Should().HaveCount(2);
        stored.Items.Sum(i => i.Amount).Should().Be(stored.TotalAmount);
        stored.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_OutOfScopeStudent_ThrowsNotFound()
    {
        // P1.1 scope contract: creating an invoice for a student the caller
        // cannot see is reported as NotFound (no existence leak). This test
        // verifies the contract at the integration layer — bare unit tests
        // could mock around it.
        var blockedScope = new Mock<IEffectiveScope>();
        blockedScope.Setup(s => s.CanAccessStudentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        var blockedService = new InvoiceService(
            new DbContextUnitOfWork(_db),
            _repo,
            new CreateInvoiceValidator(),
            _cache,
            blockedScope.Object,
            new TestLocalizationService());

        await blockedService.Invoking(s => s.CreateAsync(new CreateInvoiceRequest
        {
            StudentId = Guid.NewGuid(),
            Currency = "EGP",
            Items = { new CreateInvoiceItemRequest { Amount = 100m, FeeType = "Tuition", SourceModule = "registration" } },
        }))
        .Should().ThrowAsync<CapitalUniversity.Core.Domain.Common.Exceptions.NotFoundException>();

        (await _db.Set<Invoice>().AnyAsync()).Should().BeFalse(
            "an out-of-scope rejection must not leave a partial row");
    }

    [Fact]
    public async Task GetForStudent_ReturnsOnlyThatStudentsInvoices()
    {
        // Repository-layer round-trip: persistent rows are partitioned by
        // StudentId. Pins the index-supported per-student list query that
        // the student portal hits on every login.
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await _service.CreateAsync(NewSingleItemInvoice(alice, 100m));
        await _service.CreateAsync(NewSingleItemInvoice(alice, 200m));
        await _service.CreateAsync(NewSingleItemInvoice(bob,   50m));

        var aliceRows = await _repo.GetForStudentAsync(alice);
        var bobRows = await _repo.GetForStudentAsync(bob);

        aliceRows.Should().HaveCount(2);
        bobRows.Should().HaveCount(1);
        aliceRows.Should().AllSatisfy(i => i.StudentId.Should().Be(alice));
        bobRows.Single().TotalAmount.Should().Be(50m);
    }

    [Fact]
    public async Task GetByIdAsync_IncludeItems_HydratesItemCollection()
    {
        // Include-items round-trip: explicit Include(i => i.Items) must
        // attach the items in a single shaped result (no N+1 lazy-load
        // dependence; the production DbContext has lazy loading off).
        var studentId = Guid.NewGuid();
        var invoiceId = await _service.CreateAsync(new CreateInvoiceRequest
        {
            StudentId = studentId,
            Currency = "EGP",
            Items =
            {
                new CreateInvoiceItemRequest { Amount = 10m, FeeType = "A", SourceModule = "registration" },
                new CreateInvoiceItemRequest { Amount = 20m, FeeType = "B", SourceModule = "registration" },
                new CreateInvoiceItemRequest { Amount = 30m, FeeType = "C", SourceModule = "registration" },
            },
        });

        var loaded = await _repo.GetByIdAsync(invoiceId, includeItems: true);

        loaded.Should().NotBeNull();
        loaded!.Items.Should().HaveCount(3);
        loaded.Items.Sum(i => i.Amount).Should().Be(loaded.TotalAmount);
    }

    private static CreateInvoiceRequest NewSingleItemInvoice(Guid studentId, decimal amount) => new()
    {
        StudentId = studentId,
        Currency = "EGP",
        Items =
        {
            new CreateInvoiceItemRequest { Amount = amount, FeeType = "Tuition", SourceModule = "registration", Description = "x" },
        },
    };

    // ── Test doubles ─────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal IUnitOfWork that forwards SaveChangesAsync to the supplied
    /// CoreDbContext and treats ExecuteInSerializableTransactionAsync as a
    /// no-op (matches the production UoW's InMemory branch, see
    /// UnitOfWork.cs:54-61). The seven repository properties are unused by
    /// the InvoiceService so they remain null.
    /// </summary>
    private sealed class DbContextUnitOfWork : IUnitOfWork
    {
        private readonly CoreDbContext _context;
        public DbContextUnitOfWork(CoreDbContext context) => _context = context;

        public IStudentRepository Students => null!;
        public IStaffRepository Staff => null!;
        public IStructureNodeRepository StructureNodes => null!;
        public IAcademicYearRepository AcademicYears => null!;
        public ISemesterRepository Semesters => null!;
        public ICourseRepository Courses => null!;
        public IAcademicPlanRepository AcademicPlans => null!;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);

        public Task ExecuteInSerializableTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) =>
            action(cancellationToken);

        public void Dispose() { /* CoreDbContext is disposed by the test class. */ }
    }

    private sealed class StubCache : ICacheService
    {
        public int RemoveCalls;
        public Task<T?> GetAsync<T>(string key, CancellationToken c = default) => Task.FromResult(default(T?));
        public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken c = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken c = default) { RemoveCalls++; return Task.CompletedTask; }
    }
}

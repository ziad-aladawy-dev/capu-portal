using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Modules.Payments.Application;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Payments.Repositories;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Payments;

public class FeeCreationServiceTests
{
    private static FeeCreationService Build(
        out Mock<IUnitOfWork> uow,
        out Mock<IInvoiceRepository> invoices,
        out Mock<ICacheService> cache)
    {
        uow = new Mock<IUnitOfWork>();
        invoices = new Mock<IInvoiceRepository>();
        cache = new Mock<ICacheService>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return new FeeCreationService(uow.Object, invoices.Object, cache.Object);
    }

    private static CreateInvoiceItemRequest Item(decimal amount, string type = "Tuition") =>
        new() { Amount = amount, FeeType = type, SourceModule = "academics", Description = "x" };

    [Fact]
    public async Task CreateFees_NoItems_ThrowsArgumentException()
    {
        var sut = Build(out _, out _, out _);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.CreateFeesAsync(Guid.NewGuid(), "EGP", Array.Empty<CreateInvoiceItemRequest>()));
    }

    [Fact]
    public async Task CreateFees_NoMerge_CreatesNewInvoiceAndPersistsItems()
    {
        var sut = Build(out var uow, out var invoices, out var cache);
        Invoice? captured = null;
        invoices.Setup(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
                .Callback<Invoice, CancellationToken>((inv, _) => captured = inv)
                .Returns(Task.CompletedTask);

        var studentId = Guid.NewGuid();
        var due = DateTime.UtcNow.AddDays(7);

        var id = await sut.CreateFeesAsync(
            studentId, "EGP",
            new[] { Item(100m), Item(50m, "Library") },
            mergeWithPending: false,
            dueAt: due);

        Assert.NotNull(captured);
        Assert.Equal(studentId, captured!.StudentId);
        Assert.Equal("EGP", captured.Currency);
        Assert.Equal(InvoiceStatus.Pending, captured.Status);
        Assert.Equal(due, captured.DueAt);
        Assert.Equal(2, captured.Items.Count);
        Assert.Equal(150m, captured.TotalAmount);
        Assert.Equal(captured.Id, id);

        invoices.Verify(r => r.Update(captured), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        // CacheKey() is internal to the Payments module; the contract we observe is
        // that some cache-remove happened for the captured invoice's id.
        cache.Verify(c => c.RemoveAsync(It.Is<string>(k => k.Contains(captured.Id.ToString("N"))),
                                        It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateFees_MergeWithPending_NoneFound_FallsBackToNewInvoice()
    {
        var sut = Build(out _, out var invoices, out _);
        invoices.Setup(r => r.GetPendingForStudentAsync(It.IsAny<Guid>(), "EGP", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Invoice?)null);
        invoices.Setup(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await sut.CreateFeesAsync(Guid.NewGuid(), "EGP", new[] { Item(10m) }, mergeWithPending: true);

        invoices.Verify(r => r.GetPendingForStudentAsync(It.IsAny<Guid>(), "EGP", It.IsAny<CancellationToken>()), Times.Once);
        invoices.Verify(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateFees_MergeWithPending_ExistingFound_AppendsAndDoesNotAdd()
    {
        var sut = Build(out _, out var invoices, out _);
        var existing = new Invoice
        {
            StudentId = Guid.NewGuid(),
            Currency = "EGP",
            Status = InvoiceStatus.Pending,
            TotalAmount = 25m,
            Items = new List<InvoiceItem> { new() { Amount = 25m } }
        };
        invoices.Setup(r => r.GetPendingForStudentAsync(existing.StudentId, "EGP", It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

        var id = await sut.CreateFeesAsync(existing.StudentId, "EGP",
            new[] { Item(75m) }, mergeWithPending: true);

        invoices.Verify(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(2, existing.Items.Count);
        Assert.Equal(100m, existing.TotalAmount);
        Assert.Equal(existing.Id, id);
    }
}

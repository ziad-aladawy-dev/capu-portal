using CapitalUniversity.Core.Abstractions.Payments;
using CapitalUniversity.Core.Abstractions.Payments.DTOs;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Domain.Payments;

namespace CapitalUniversity.Core.Application.Payments;

/// <summary>
/// The seam upstream modules use to author fees. Centralises both code paths
/// (new invoice vs append-to-pending) so callers do not have to know how the
/// payments module composes lines. Invalidates the shared-object cache key
/// when items land on an existing invoice.
/// </summary>
public class FeeCreationService : IFeeCreationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvoiceRepository _invoices;
    private readonly ICacheService _cache;

    public FeeCreationService(IUnitOfWork unitOfWork, IInvoiceRepository invoices, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _invoices = invoices;
        _cache = cache;
    }

    public async Task<Guid> CreateFeesAsync(
        Guid studentId,
        string currency,
        IReadOnlyCollection<CreateInvoiceItemRequest> items,
        bool mergeWithPending = false,
        DateTime? dueAt = null,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            throw new ArgumentException("At least one item is required.", nameof(items));

        Invoice? invoice = null;
        if (mergeWithPending)
        {
            invoice = await _invoices.GetPendingForStudentAsync(studentId, currency, cancellationToken);
        }

        if (invoice is null)
        {
            invoice = new Invoice
            {
                StudentId = studentId,
                Currency = currency,
                DueAt = dueAt,
                Status = InvoiceStatus.Pending,
            };
            await _invoices.AddAsync(invoice, cancellationToken);
        }

        foreach (var i in items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Amount = i.Amount,
                FeeType = i.FeeType,
                SourceModule = i.SourceModule,
                ReferenceId = i.ReferenceId,
                Description = i.Description,
            });
        }
        invoice.TotalAmount = invoice.Items.Sum(it => it.Amount);
        invoice.UpdatedAt = DateTime.UtcNow;
        _invoices.Update(invoice);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Existing invoice — drop the cached payload so the next read picks up new items.
        await _cache.RemoveAsync(InvoiceService.CacheKey(invoice.Id), cancellationToken);
        return invoice.Id;
    }
}

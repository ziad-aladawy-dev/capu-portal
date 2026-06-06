using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Modules.Payments.Application.Validators;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Payments.Repositories;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;


namespace CapitalUniversity.Modules.Payments.Application;

/// <summary>
/// The seam upstream modules use to author fees. Centralises both code paths
/// (new invoice vs append-to-pending) so callers do not have to know how the
/// payments module composes lines. Invalidates the shared-object cache key
/// when items land on an existing invoice.
/// </summary>
public class FeeCreationService : IFeeCreationService
{
    // M8 — every item is validated through the same rules as direct-create
    // invoice items. Cached statically because FluentValidation validators
    // are stateless and the rules never change.
    private static readonly CreateInvoiceItemValidator ItemValidator = new();

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
            throw new ArgumentException(LocalizedKeys.Payments.AtLeastOneItem, nameof(items));

        // M8 — service-to-service callers (other modules) historically
        // bypassed the controller's FluentValidation pipeline, so negative or
        // oversized amounts could land directly on an invoice. Run the same
        // validator the controller uses, aggregate errors per item index for
        // a clear "items[2].Amount" call-site message, and refuse the whole
        // batch on the first invalid entry (one bad fee shouldn't be allowed
        // to partially commit).
        var errors = new Dictionary<string, string[]>();
        var idx = 0;
        foreach (var item in items)
        {
            var result = await ItemValidator.ValidateAsync(item, cancellationToken);
            if (!result.IsValid)
            {
                foreach (var error in result.Errors)
                {
                    var key = $"items[{idx}].{error.PropertyName}";
                    if (errors.TryGetValue(key, out var existing))
                    {
                        errors[key] = existing.Append(error.ErrorMessage).ToArray();
                    }
                    else
                    {
                        errors[key] = new[] { error.ErrorMessage };
                    }
                }
            }
            idx++;
        }
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

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
        // Drop the cached payload so the next read picks up new items, and rotate
        // the invoice collection version so list/search reads reflect the new or
        // newly-larger invoice.
        await _cache.RemoveAsync(InvoiceService.CacheKey(invoice.Id), cancellationToken);
        await InvoiceService.BumpCollectionVersionAsync(_cache, cancellationToken);
        return invoice.Id;
    }
}

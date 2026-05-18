using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Payments;
using CapitalUniversity.Core.Abstractions.Payments.DTOs;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Payments;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.Application.Payments;

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvoiceRepository _invoices;
    private readonly IValidator<RecordPaymentRequest> _validator;
    private readonly ICacheService _cache;

    public PaymentVerificationService(
        IUnitOfWork unitOfWork,
        IInvoiceRepository invoices,
        IValidator<RecordPaymentRequest> validator,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _invoices = invoices;
        _validator = validator;
        _cache = cache;
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

        var existing = await _invoices.GetTransactionByKeyAsync(request.InvoiceId, request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            // Replay — return the previously recorded outcome verbatim.
            return ToResponse(existing);
        }

        var invoice = await _invoices.GetByIdAsync(request.InvoiceId, includeItems: false, includeTransactions: true, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync(InvoiceService.CacheKey(invoice.Id), cancellationToken);
        return ToResponse(tx);
    }

    public async Task<IReadOnlyList<PaymentTransactionResponse>> GetForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var rows = await _invoices.GetTransactionsForInvoiceAsync(invoiceId, cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    private static void ReflectSettledTotal(Invoice invoice)
    {
        var settled = invoice.Transactions
            .Where(t => t.Status == PaymentTransactionStatus.Succeeded)
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

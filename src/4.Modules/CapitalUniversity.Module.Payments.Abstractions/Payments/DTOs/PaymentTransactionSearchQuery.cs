using CapitalUniversity.Core.Abstractions.Shared.Paging;

namespace CapitalUniversity.Modules.Payments.Abstractions.DTOs;

/// <summary>
/// Filters / pagination / sort for the payment transactions list.
/// Sort whitelist: <c>createdAt</c>, <c>amount</c>, <c>status</c>.
/// </summary>
public class PaymentTransactionSearchQuery : PagedQueryRequest
{
    public Guid? InvoiceId { get; set; }
    public Guid? StudentId { get; set; }
    public PaymentTransactionStatus? Status { get; set; }
    public string? Provider { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}

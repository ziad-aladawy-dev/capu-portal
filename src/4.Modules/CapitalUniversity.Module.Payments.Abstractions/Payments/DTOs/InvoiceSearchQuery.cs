using CapitalUniversity.Core.Abstractions.Shared.Paging;

namespace CapitalUniversity.Modules.Payments.Abstractions.DTOs;

/// <summary>
/// Filter / pagination / sort for the invoice list endpoint. Every filter
/// other than <see cref="Search"/> is optional and ANDs with the rest.
/// </summary>
/// <remarks>
/// Sort whitelist: <c>createdAt</c>, <c>dueAt</c>, <c>amount</c>, <c>status</c>.
/// Default order: <c>createdAt:desc</c>.
/// Date params: <c>*From</c> inclusive, <c>*To</c> exclusive.
/// </remarks>
public class InvoiceSearchQuery : PagedQueryRequest
{
    public Guid? StudentId { get; set; }
    public InvoiceStatus? Status { get; set; }

    public DateTime? IssuedFrom { get; set; }
    public DateTime? IssuedTo { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }

    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public string? Currency { get; set; }
}

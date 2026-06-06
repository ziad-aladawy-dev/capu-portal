using CapitalUniversity.Modules.Payments.Abstractions;

namespace CapitalUniversity.Sync.Host.Admin;

/// <summary>
/// Body for <c>POST /admin/outbox/finance/{externalInvoiceId}</c>. Every field
/// is optional — omitted fields fall back to canned defaults so a bare
/// curl-without-body still seeds a valid Pending row.
/// </summary>
public sealed class InvoiceOutboxSeedRequest
{
    public string? ExternalStudentId { get; set; }
    public InvoiceStatus? Status { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Currency { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTimeOffset? ExternalUpdatedAt { get; set; }
    public int? ExternalVersion { get; set; }
}

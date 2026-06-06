using CapitalUniversity.Modules.Payments.Abstractions;

namespace CapitalUniversity.Sync.Finance.Domain;

/// <summary>
/// External shape from the upstream bursar system. Mirrors Core's Invoice
/// header (header-only sync; line items are out of scope for this module).
/// <see cref="ExternalStudentId"/> is the upstream student key — the gateway
/// resolves it to <c>Student.Id</c> via <c>ICoreWriteGateway.ResolveIdByExternalIdAsync</c>.
/// </summary>
public sealed class ExternalInvoice
{
    public required string ExternalInvoiceId { get; init; }
    public required string ExternalStudentId { get; init; }
    public required InvoiceStatus Status { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public DateTime? DueAt { get; init; }
    public required DateTimeOffset ExternalUpdatedAt { get; init; }
    public required int ExternalVersion { get; init; }
}

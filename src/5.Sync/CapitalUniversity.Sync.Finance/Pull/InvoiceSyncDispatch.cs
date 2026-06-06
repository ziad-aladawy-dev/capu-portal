using CapitalUniversity.Modules.Payments.Domain;

namespace CapitalUniversity.Sync.Finance.Pull;

/// <summary>
/// Pipeline carrier for the Finance pull. Bundles the mapped Core
/// <see cref="Invoice"/> entity with the upstream student key that
/// <see cref="InvoiceWriter"/> needs to resolve into <c>Invoice.StudentId</c>
/// before forwarding to <c>ICoreWriteGateway</c>.
/// <para>
/// Mapper and validator both operate on this wrapper so the upstream key
/// stays attached to the entity it belongs to all the way through the
/// batch — keeping the pipeline contract intact (one external row ↔ one
/// internal record) without smuggling an out-of-band side table.
/// </para>
/// </summary>
public sealed class InvoiceSyncDispatch
{
    public required Invoice Entity { get; init; }

    /// <summary>Upstream student key — resolved to <c>Invoice.StudentId</c> in the writer.</summary>
    public required string ExternalStudentId { get; init; }
}

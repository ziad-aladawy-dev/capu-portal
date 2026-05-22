

using CapitalUniversity.Modules.Payments.Abstractions.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions;

/// <summary>
/// Contract used by upstream modules (registration, library, housing, …) to
/// author fees against a student. Payments is the only module that owns the
/// invoice schema; other modules call into this interface and treat the
/// resulting <see cref="InvoiceResponse.Id"/> as opaque.
///
/// <para>
/// Per <c>docs/Plan.md</c> the Payments module is the centralised financial
/// infrastructure — never write directly into Invoice / InvoiceItem from
/// outside this seam.
/// </para>
/// </summary>
public interface IFeeCreationService
{
    /// <summary>
    /// Creates a fresh invoice for the student with the supplied items, or
    /// appends the items to an existing pending invoice if one already
    /// exists for the same <c>(StudentId, Currency)</c>. Caller chooses
    /// the policy via <paramref name="mergeWithPending"/>.
    /// </summary>
    Task<Guid> CreateFeesAsync(
        Guid studentId,
        string currency,
        IReadOnlyCollection<CreateInvoiceItemRequest> items,
        bool mergeWithPending = false,
        DateTime? dueAt = null,
        CancellationToken cancellationToken = default);
}

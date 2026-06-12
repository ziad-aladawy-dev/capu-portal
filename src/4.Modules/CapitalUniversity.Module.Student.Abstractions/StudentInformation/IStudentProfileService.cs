using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation.DTOs;

namespace CapitalUniversity.Modules.Student.Abstractions.StudentInformation;

/// <summary>
/// Manages flexible profile records for a student. The service owns the
/// "what records does this student have" view; consuming modules parse
/// <see cref="StudentProfileRecordResponse.DataJson"/> per their own
/// <see cref="StudentProfileRecordResponse.SchemaVersion"/>.
/// </summary>
public interface IStudentProfileService
{
    Task<StudentProfileRecordResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentProfileRecordResponse>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<StudentProfileRecordResponse?> GetForStudentCategoryAsync(Guid studentId, StudentProfileCategory category, string? customCategoryKey = null, CancellationToken cancellationToken = default);
    Task<Guid> UpsertAsync(Guid studentId, UpsertStudentProfileRecordRequest request, CancellationToken cancellationToken = default);
    // C1 — studentId is required: the service asserts the record belongs to that
    // student before any mutation. A mismatch surfaces as NotFound so callers
    // cannot distinguish "wrong owner" from "record absent" (no enumeration).
    Task VerifyAsync(Guid studentId, Guid id, VerifyStudentProfileRecordRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid studentId, Guid id, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid studentId, Guid id, CancellationToken cancellationToken = default);
    Task OpenRecordAsync(Guid studentId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 3.5 — bulk upsert. Routes each record through the single-row
    /// <see cref="UpsertAsync"/> so scope + validation + re-verification clear
    /// stay identical. Independent per-row commits — a failed peer does not
    /// roll back successes.
    /// </summary>
    Task<BulkActionResult> BatchUpsertAsync(Guid studentId, IReadOnlyList<UpsertStudentProfileRecordRequest> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// 3.6 — bulk verify. <paramref name="verifiedBy"/> applies to every row in
    /// the batch. Same per-row semantics as the bulk upsert (independent
    /// commits, per-row failure reasons).
    /// </summary>
    Task<BulkActionResult> BatchVerifyAsync(Guid studentId, IReadOnlyList<Guid> recordIds, Guid verifiedBy, CancellationToken cancellationToken = default);
}

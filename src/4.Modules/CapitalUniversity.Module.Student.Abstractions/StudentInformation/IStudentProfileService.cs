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
}

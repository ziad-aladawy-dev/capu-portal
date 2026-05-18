using CapitalUniversity.Core.Abstractions.StudentInformation.DTOs;
using CapitalUniversity.Core.Domain.StudentInformation;

namespace CapitalUniversity.Core.Abstractions.StudentInformation;

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
    Task VerifyAsync(Guid id, VerifyStudentProfileRecordRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

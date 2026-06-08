using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;
using CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;
using CapitalUniversity.Modules.AcademicRecords.Repositories;

namespace CapitalUniversity.Modules.AcademicRecords.Application;

/// <summary>
/// Read-only query surface over the academic-records read-model — grade history,
/// per-semester detail, and the academic summary. Deliberately narrow: no grade
/// entry/modification, no GPA/CGPA calculation, no registration management (all
/// out of scope per <c>docs/Grades/Grades_Model.md</c>).
///
/// <para>
/// Row-level access is enforced through <see cref="IEffectiveScope.CanAccessStudentAsync"/>
/// before any data is returned: a student passes only for their own id; staff
/// pass when one of their authorized path prefixes covers the student's
/// structure node. Out-of-scope reads return an empty / null result (no existence
/// leak), consistent with the Registration / StudentServiceRequest services.
/// </para>
/// </summary>
public class AcademicRecordsService : IAcademicRecordsService
{
    private readonly IAcademicRecordsRepository _records;
    private readonly IEffectiveScope _scope;

    public AcademicRecordsService(IAcademicRecordsRepository records, IEffectiveScope scope)
    {
        _records = records;
        _scope = scope;
    }

    public async Task<IReadOnlyList<SemesterHistoryDto>> GetStudentSemesterHistoryAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return Array.Empty<SemesterHistoryDto>();
        }

        return await _records.GetSemesterHistoryForStudentAsync(studentId, cancellationToken);
    }

    public async Task<IReadOnlyList<SemesterCourseDto>> GetSemesterGradesAsync(
        Guid studentId,
        Guid semesterId,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return Array.Empty<SemesterCourseDto>();
        }

        return await _records.GetSemesterGradesForStudentAsync(studentId, semesterId, cancellationToken);
    }

    public async Task<AcademicSummaryDto?> GetAcademicSummaryAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return null;
        }

        return await _records.GetLatestSummaryForStudentAsync(studentId, cancellationToken);
    }
}

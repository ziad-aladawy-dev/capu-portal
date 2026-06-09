using CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;

namespace CapitalUniversity.Modules.AcademicRecords.Abstractions;

/// <summary>
/// Transcript generation over a student's synchronized academic outcomes. Builds
/// the requirement-category transcript structure from <c>StudentAcademicResult</c>
/// + Academic Plan + Registered Courses, and renders it to PDF. Only
/// latest-attempt courses appear (historical attempts stay in grade history).
///
/// <para>
/// Both methods enforce that a student may only access their own transcript
/// (staff within their structure-node scope) via <c>IEffectiveScope</c>;
/// out-of-scope reads return <c>null</c> rather than leaking existence.
/// </para>
/// </summary>
public interface ITranscriptService
{
    /// <summary>
    /// The student's transcript structure (summary header + courses grouped into
    /// requirement categories), or <c>null</c> when out of the caller's scope.
    /// </summary>
    Task<TranscriptDto?> GetStudentTranscriptAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The student's transcript rendered as a PDF, or <c>null</c> when out of the
    /// caller's scope.
    /// </summary>
    Task<TranscriptPdfDto?> GenerateStudentTranscriptPdfAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}

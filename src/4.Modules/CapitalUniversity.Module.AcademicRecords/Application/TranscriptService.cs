using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;
using CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;
using CapitalUniversity.Modules.AcademicRecords.Application.Pdf;
using CapitalUniversity.Modules.AcademicRecords.Repositories;

namespace CapitalUniversity.Modules.AcademicRecords.Application;

/// <summary>
/// Builds a student's transcript from <c>StudentAcademicResult</c> + Academic
/// Plan + Registered Courses, and renders it to PDF. Only latest-attempt courses
/// are included (historical attempts stay in grade history); every latest-attempt
/// course appears regardless of status, with a <c>"-"</c> grade for any
/// non-completed attempt — per the doc's transcript rules.
///
/// <para>
/// Requirement categories are derived from the catalog <see cref="CourseCategory"/>
/// (the available plan/catalog categorization); Compulsory vs Elective is taken
/// from the student's active academic plan, falling back to the catalog Elective
/// category when a course is not in any active plan. Access is scope-gated via
/// <see cref="IEffectiveScope"/>; out-of-scope reads return <c>null</c>.
/// </para>
/// </summary>
public class TranscriptService : ITranscriptService
{
    private readonly IAcademicRecordsRepository _records;
    private readonly IEffectiveScope _scope;
    private readonly ITranscriptPdfRenderer _pdfRenderer;

    public TranscriptService(
        IAcademicRecordsRepository records,
        IEffectiveScope scope,
        ITranscriptPdfRenderer pdfRenderer)
    {
        _records = records;
        _scope = scope;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<TranscriptDto?> GetStudentTranscriptAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return null;
        }

        var rows = await _records.GetTranscriptRowsForStudentAsync(studentId, cancellationToken);

        var structureNodeIds = rows.Select(r => r.StructureNodeId).Distinct().ToArray();
        var mandatoryMap = await _records.GetPlanMandatoryMapAsync(structureNodeIds, cancellationToken);

        var summary = await _records.GetLatestSummaryForStudentAsync(studentId, cancellationToken);
        var identity = await _records.GetStudentIdentityAsync(studentId, cancellationToken);

        var courses = rows.Select(r => new
        {
            Category = MapCategory(r.Category),
            Dto = new TranscriptCourseDto
            {
                CourseId = r.CourseId,
                CourseCode = r.CourseCode,
                CourseTitle = r.CourseTitle,
                CreditHours = r.CreditHours,
                Grade = DisplayGrade(r.Status, r.Grade),
                NumericScore = r.NumericScore,
                Status = r.Status,
                CreditsEarned = r.CreditsEarned,
                IsElective = IsElective(r.CourseId, r.Category, mandatoryMap),
            },
        }).ToList();

        // Emit all three categories in canonical order so the transcript shape is
        // stable for clients even when a category has no courses.
        var categories = new[]
        {
            TranscriptRequirementCategory.General,
            TranscriptRequirementCategory.Faculty,
            TranscriptRequirementCategory.MainSpecialization,
        }.Select(cat =>
        {
            var inCat = courses.Where(c => c.Category == cat).Select(c => c.Dto).ToList();
            return new TranscriptCategoryDto
            {
                Category = cat,
                DisplayName = DisplayNameFor(cat),
                Compulsory = inCat.Where(c => !c.IsElective).ToList(),
                Elective = inCat.Where(c => c.IsElective).ToList(),
            };
        }).ToList();

        return new TranscriptDto
        {
            StudentId = studentId,
            StudentName = identity?.Name ?? string.Empty,
            StudentCode = identity?.StudentCode ?? string.Empty,
            Summary = summary,
            Categories = categories,
        };
    }

    public async Task<TranscriptPdfDto?> GenerateStudentTranscriptPdfAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var transcript = await GetStudentTranscriptAsync(studentId, cancellationToken);
        if (transcript is null)
        {
            return null;
        }

        var bytes = _pdfRenderer.Render(transcript);
        return new TranscriptPdfDto
        {
            Content = bytes,
            FileName = $"transcript-{studentId}.pdf",
            ContentType = "application/pdf",
        };
    }

    /// <summary>
    /// Maps the catalog <see cref="CourseCategory"/> to a transcript requirement
    /// category. University-wide + general-education courses are General;
    /// faculty courses are Faculty; program courses are Main Specialization.
    /// Pure-elective / unspecified courses have no requirement type of their own,
    /// so they fall under General (their compulsory/elective split still comes
    /// from the plan).
    /// </summary>
    private static TranscriptRequirementCategory MapCategory(CourseCategory category) => category switch
    {
        CourseCategory.ProgramRequirement => TranscriptRequirementCategory.MainSpecialization,
        CourseCategory.FacultyRequirement => TranscriptRequirementCategory.Faculty,
        CourseCategory.UniversityRequirement => TranscriptRequirementCategory.General,
        CourseCategory.GeneralEducation => TranscriptRequirementCategory.General,
        _ => TranscriptRequirementCategory.General,
    };

    /// <summary>
    /// Elective when the student's active plan marks the course non-mandatory;
    /// for courses absent from any active plan, falls back to the catalog Elective
    /// category.
    /// </summary>
    private static bool IsElective(Guid courseId, CourseCategory category, IReadOnlyDictionary<Guid, bool> mandatoryMap) =>
        mandatoryMap.TryGetValue(courseId, out var isMandatory)
            ? !isMandatory
            : category == CourseCategory.Elective;

    /// <summary>
    /// Completed attempts (passed / failed) carry their synced letter grade;
    /// every non-completed attempt shows <c>"-"</c> per the doc's status rules.
    /// </summary>
    private static string DisplayGrade(AcademicResultStatus status, string? grade) =>
        status is AcademicResultStatus.Passed or AcademicResultStatus.Failed
            ? grade ?? "-"
            : "-";

    private static string DisplayNameFor(TranscriptRequirementCategory category) => category switch
    {
        TranscriptRequirementCategory.General => "General Requirements",
        TranscriptRequirementCategory.Faculty => "Faculty Requirements",
        TranscriptRequirementCategory.MainSpecialization => "Main Specialization Requirements",
        _ => category.ToString(),
    };
}

using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;
using CapitalUniversity.Modules.AcademicRecords.Domain;
using CapitalUniversity.Modules.Registration.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Modules.AcademicRecords.Repositories;

/// <summary>
/// Read-only projections over the academic-records read-model. Each query folds
/// result ⋈ registration ⋈ course ⋈ semester into one SQL statement (no N+1);
/// grouping that EF cannot translate is done over the already-ordered
/// materialized list so term/course ordering is preserved without a second
/// round-trip.
/// </summary>
public class AcademicRecordsRepository : IAcademicRecordsRepository
{
    private readonly CoreDbContext _context;

    public AcademicRecordsRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SemesterHistoryDto>> GetSemesterHistoryForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        // result ⋈ registration ⋈ course ⋈ semester, carrying the term ordering
        // metadata so the in-memory grouping below preserves Latest → Oldest order
        // without a second round-trip. Newest term first (StartDate desc).
        var rows = await (
            from res in _context.Set<StudentAcademicResult>().AsNoTracking()
            join reg in _context.Set<StudentRegisteredCourse>().AsNoTracking() on res.StudentRegisteredCourseId equals reg.Id
            join c in _context.Set<Course>().AsNoTracking() on reg.CourseId equals c.Id
            join s in _context.Set<Semester>().AsNoTracking() on reg.SemesterId equals s.Id
            where reg.StudentId == studentId
            orderby s.StartDate descending, c.Code, reg.AttemptNumber
            select new HistoryRow
            {
                SemesterId = reg.SemesterId,
                SemesterName = s.Name,
                AcademicYearId = s.AcademicYearId,
                Order = s.Order,
                Course = new SemesterCourseDto
                {
                    Id = res.Id,
                    StudentRegisteredCourseId = res.StudentRegisteredCourseId,
                    CourseId = reg.CourseId,
                    CourseCode = c.Code,
                    CourseTitle = c.Title,
                    CreditHours = c.CreditHours,
                    AttemptNumber = reg.AttemptNumber,
                    IsLatestAttempt = res.IsLatestAttempt,
                    Grade = res.Grade,
                    NumericScore = res.NumericScore,
                    Status = res.Status,
                    CreditsEarned = res.CreditsEarned,
                    SyncedAt = res.ExternallySourced.LastSyncedAt,
                },
            }).ToListAsync(cancellationToken);

        // GroupBy over the already-ordered list keeps both the term order (first
        // appearance = newest) and the per-term course order intact.
        return rows
            .GroupBy(x => x.SemesterId)
            .Select(g =>
            {
                var first = g.First();
                return new SemesterHistoryDto
                {
                    SemesterId = g.Key,
                    SemesterName = first.SemesterName,
                    AcademicYearId = first.AcademicYearId,
                    Order = first.Order,
                    Courses = g.Select(x => x.Course).ToList(),
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SemesterCourseDto>> GetSemesterGradesForStudentAsync(
        Guid studentId,
        Guid semesterId,
        CancellationToken cancellationToken = default) =>
        await (
            from res in _context.Set<StudentAcademicResult>().AsNoTracking()
            join reg in _context.Set<StudentRegisteredCourse>().AsNoTracking() on res.StudentRegisteredCourseId equals reg.Id
            join c in _context.Set<Course>().AsNoTracking() on reg.CourseId equals c.Id
            where reg.StudentId == studentId && reg.SemesterId == semesterId
            orderby c.Code, reg.AttemptNumber
            select new SemesterCourseDto
            {
                Id = res.Id,
                StudentRegisteredCourseId = res.StudentRegisteredCourseId,
                CourseId = reg.CourseId,
                CourseCode = c.Code,
                CourseTitle = c.Title,
                CreditHours = c.CreditHours,
                AttemptNumber = reg.AttemptNumber,
                IsLatestAttempt = res.IsLatestAttempt,
                Grade = res.Grade,
                NumericScore = res.NumericScore,
                Status = res.Status,
                CreditsEarned = res.CreditsEarned,
                SyncedAt = res.ExternallySourced.LastSyncedAt,
            }).ToListAsync(cancellationToken);

    public async Task<AcademicSummaryDto?> GetLatestSummaryForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        await _context.Set<AcademicSummarySnapshot>().AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.ExternallySourced.LastSyncedAt)
            .Select(x => new AcademicSummaryDto
            {
                StudentId = x.StudentId,
                Gpa = x.Gpa,
                Cgpa = x.Cgpa,
                EarnedCredits = x.EarnedCredits,
                RemainingCredits = x.RemainingCredits,
                PassedHours = x.PassedHours,
                FailedHours = x.FailedHours,
                AcademicStanding = x.AcademicStanding,
                SyncedAt = x.ExternallySourced.LastSyncedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<StudentIdentity?> GetStudentIdentityAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        await _context.Set<Student>().AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new StudentIdentity { Name = s.Name, StudentCode = s.StudentCode })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TranscriptSourceRow>> GetTranscriptRowsForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        await (
            from res in _context.Set<StudentAcademicResult>().AsNoTracking()
            join reg in _context.Set<StudentRegisteredCourse>().AsNoTracking() on res.StudentRegisteredCourseId equals reg.Id
            join c in _context.Set<Course>().AsNoTracking() on reg.CourseId equals c.Id
            where reg.StudentId == studentId && res.IsLatestAttempt
            orderby c.Code
            select new TranscriptSourceRow
            {
                CourseId = reg.CourseId,
                CourseCode = c.Code,
                CourseTitle = c.Title,
                CreditHours = c.CreditHours,
                Category = c.Category,
                StructureNodeId = reg.StructureNodeId,
                Grade = res.Grade,
                NumericScore = res.NumericScore,
                Status = res.Status,
                CreditsEarned = res.CreditsEarned,
            }).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, bool>> GetPlanMandatoryMapAsync(
        IReadOnlyCollection<Guid> structureNodeIds,
        CancellationToken cancellationToken = default)
    {
        if (structureNodeIds.Count == 0)
        {
            return new Dictionary<Guid, bool>();
        }

        // Active, open plans for the student's structure node(s) ⋈ their course
        // composition. A course mandatory in any of those plans is treated as
        // compulsory (mandatory-wins) for the transcript split.
        var pairs = await (
            from p in _context.Set<AcademicPlan>().AsNoTracking()
            join pc in _context.Set<AcademicPlanCourse>().AsNoTracking() on p.Id equals pc.AcademicPlanId
            where structureNodeIds.Contains(p.StructureNodeId) && p.IsActive && !p.IsClosed
            select new { pc.CourseId, pc.IsMandatory })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(x => x.CourseId)
            .ToDictionary(g => g.Key, g => g.Any(x => x.IsMandatory));
    }

    /// <summary>Internal carrier pairing a projected course result with its term's ordering metadata for grouping.</summary>
    private sealed class HistoryRow
    {
        public Guid SemesterId { get; init; }
        public string SemesterName { get; init; } = string.Empty;
        public Guid AcademicYearId { get; init; }
        public int Order { get; init; }
        public SemesterCourseDto Course { get; init; } = null!;
    }
}

using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Registration.Abstractions;
using CapitalUniversity.Modules.Registration.Abstractions.DTOs;
using CapitalUniversity.Modules.Registration.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Modules.Registration.Repositories;

public class StudentRegistrationRepository : IStudentRegistrationRepository
{
    private readonly CoreDbContext _context;

    public StudentRegistrationRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RegisteredCourseDto>> GetActiveForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        await BuildRegisteredCourseQuery(
                r => r.StudentId == studentId && r.RegistrationStatus == RegistrationStatus.Enrolled)
            .OrderBy(d => d.CourseCode)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SemesterRegistrationDto>> GetHistoryForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        // Single projected query: registration ⋈ course ⋈ semester, carrying the
        // semester ordering metadata so the in-memory grouping below preserves
        // academic-timeline order without a second round-trip.
        var rows = await (
            from r in _context.Set<StudentRegisteredCourse>().AsNoTracking()
            join c in _context.Set<Course>().AsNoTracking() on r.CourseId equals c.Id
            join s in _context.Set<Semester>().AsNoTracking() on r.SemesterId equals s.Id
            where r.StudentId == studentId
            orderby s.StartDate, c.Code, r.AttemptNumber
            select new HistoryRow
            {
                AcademicYearId = s.AcademicYearId,
                Order = s.Order,
                Course = new RegisteredCourseDto
                {
                    Id = r.Id,
                    StudentId = r.StudentId,
                    CourseId = r.CourseId,
                    CourseCode = c.Code,
                    CourseTitle = c.Title,
                    CreditHours = c.CreditHours,
                    SemesterId = r.SemesterId,
                    SemesterName = s.Name,
                    StructureNodeId = r.StructureNodeId,
                    AttemptNumber = r.AttemptNumber,
                    Status = r.RegistrationStatus,
                    RegisteredAt = r.RegisteredAt,
                    CompletedAt = r.CompletedAt,
                    ExternalReferenceId = r.ExternallySourced.ExternalId,
                    SyncedAt = r.ExternallySourced.LastSyncedAt,
                },
            }).ToListAsync(cancellationToken);

        // GroupBy over the already-ordered materialized list keeps both the term
        // order (first appearance) and the per-term course order intact.
        return rows
            .GroupBy(x => x.Course.SemesterId)
            .Select(g =>
            {
                var first = g.First();
                return new SemesterRegistrationDto
                {
                    SemesterId = g.Key,
                    SemesterName = first.Course.SemesterName,
                    AcademicYearId = first.AcademicYearId,
                    Order = first.Order,
                    Courses = g.Select(x => x.Course).ToList(),
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<CourseAttemptDto>> GetCourseAttemptsForStudentAsync(
        Guid studentId,
        Guid courseId,
        CancellationToken cancellationToken = default) =>
        await (
            from r in _context.Set<StudentRegisteredCourse>().AsNoTracking()
            join c in _context.Set<Course>().AsNoTracking() on r.CourseId equals c.Id
            join s in _context.Set<Semester>().AsNoTracking() on r.SemesterId equals s.Id
            where r.StudentId == studentId && r.CourseId == courseId
            orderby r.AttemptNumber
            select new CourseAttemptDto
            {
                Id = r.Id,
                CourseId = r.CourseId,
                CourseCode = c.Code,
                CourseTitle = c.Title,
                SemesterId = r.SemesterId,
                SemesterName = s.Name,
                AttemptNumber = r.AttemptNumber,
                Status = r.RegistrationStatus,
                RegisteredAt = r.RegisteredAt,
                CompletedAt = r.CompletedAt,
            }).ToListAsync(cancellationToken);

    /// <summary>
    /// Shared registration ⋈ course ⋈ semester projection. Kept as an
    /// <see cref="IQueryable{T}"/> so callers add their own predicate + ordering
    /// and EF folds the whole thing into one SQL statement (no N+1).
    /// </summary>
    private IQueryable<RegisteredCourseDto> BuildRegisteredCourseQuery(
        System.Linq.Expressions.Expression<Func<StudentRegisteredCourse, bool>> predicate) =>
        from r in _context.Set<StudentRegisteredCourse>().AsNoTracking().Where(predicate)
        join c in _context.Set<Course>().AsNoTracking() on r.CourseId equals c.Id
        join s in _context.Set<Semester>().AsNoTracking() on r.SemesterId equals s.Id
        select new RegisteredCourseDto
        {
            Id = r.Id,
            StudentId = r.StudentId,
            CourseId = r.CourseId,
            CourseCode = c.Code,
            CourseTitle = c.Title,
            CreditHours = c.CreditHours,
            SemesterId = r.SemesterId,
            SemesterName = s.Name,
            StructureNodeId = r.StructureNodeId,
            AttemptNumber = r.AttemptNumber,
            Status = r.RegistrationStatus,
            RegisteredAt = r.RegisteredAt,
            CompletedAt = r.CompletedAt,
            ExternalReferenceId = r.ExternallySourced.ExternalId,
            SyncedAt = r.ExternallySourced.LastSyncedAt,
        };

    /// <summary>Internal carrier pairing a projected course row with its term's ordering metadata for grouping.</summary>
    private sealed class HistoryRow
    {
        public Guid AcademicYearId { get; init; }
        public int Order { get; init; }
        public RegisteredCourseDto Course { get; init; } = null!;
    }
}

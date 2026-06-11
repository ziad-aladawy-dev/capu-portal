using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Courses;

namespace CapitalUniversity.Core.Application.Courses;

/// <summary>
/// Catalog prerequisite graph service. Owns the DAG invariant: every mutation
/// re-validates that no path leads from a proposed prerequisite back to the
/// dependent course, walking the full edge set (the graph is small — one row
/// per catalog dependency — so an in-memory traversal is the simplest correct
/// guard; the unique (CourseId, PrerequisiteCourseId) index is the schema
/// backstop against duplicate edges).
/// </summary>
public class CoursePrerequisiteService : ICoursePrerequisiteService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourseRepository _courses;
    private readonly ILocalizationService _localization;

    public CoursePrerequisiteService(
        IUnitOfWork unitOfWork,
        ICourseRepository courses,
        ILocalizationService localization)
    {
        _unitOfWork = unitOfWork;
        _courses = courses;
        _localization = localization;
    }

    public async Task<IReadOnlyList<CoursePrerequisiteResponse>> GetForCourseAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        _ = await _courses.GetByIdAsync(courseId, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);

        var rows = await _courses.GetPrerequisiteRowsAsync(courseId, cancellationToken);
        if (rows.Count == 0) return Array.Empty<CoursePrerequisiteResponse>();

        var prereqCourses = await _courses.GetByIdsAsync(rows.Select(r => r.PrerequisiteCourseId).ToList(), cancellationToken);
        var byId = prereqCourses.ToDictionary(c => c.Id);

        return rows
            .Select(r =>
            {
                byId.TryGetValue(r.PrerequisiteCourseId, out var course);
                return new CoursePrerequisiteResponse
                {
                    PrerequisiteCourseId = r.PrerequisiteCourseId,
                    Code = course is null ? string.Empty : _localization.Get<string>(course.Code),
                    Title = course is null ? string.Empty : _localization.Get<string>(course.Title),
                    CreditHours = course?.CreditHours ?? 0,
                    IsActive = course?.IsActive ?? false,
                };
            })
            .OrderBy(r => r.Code)
            .ToList();
    }

    public async Task<IReadOnlyList<CoursePrerequisiteLinkResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _courses.GetAllPrerequisiteRowsAsync(cancellationToken);
        return rows
            .Select(r => new CoursePrerequisiteLinkResponse
            {
                CourseId = r.CourseId,
                PrerequisiteCourseId = r.PrerequisiteCourseId,
            })
            .ToList();
    }

    public async Task SetAsync(Guid courseId, SetCoursePrerequisitesRequest request, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(courseId, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);
        course.EnsureMutable();

        var targetIds = (request.PrerequisiteCourseIds ?? new List<Guid>()).Distinct().ToList();

        if (targetIds.Contains(courseId))
            throw new ConflictException(LocalizedKeys.Courses.PrerequisiteSelfReference);

        if (targetIds.Count > 0)
        {
            var existing = await _courses.GetByIdsAsync(targetIds, cancellationToken);
            if (existing.Count != targetIds.Count)
                throw new NotFoundException(LocalizedKeys.Courses.PrerequisiteNotFound);
        }

        await EnsureAcyclicAsync(courseId, targetIds, cancellationToken);

        var current = await _courses.GetPrerequisiteRowsAsync(courseId, cancellationToken);
        var currentIds = current.Select(r => r.PrerequisiteCourseId).ToHashSet();
        var targetSet = targetIds.ToHashSet();

        foreach (var row in current.Where(r => !targetSet.Contains(r.PrerequisiteCourseId)))
            _courses.RemovePrerequisite(row);

        foreach (var id in targetIds.Where(id => !currentIds.Contains(id)))
            await _courses.AddPrerequisiteAsync(new CoursePrerequisite { CourseId = courseId, PrerequisiteCourseId = id }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(Guid courseId, AddCoursePrerequisiteRequest request, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(courseId, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);
        course.EnsureMutable();

        if (request.PrerequisiteCourseId == courseId)
            throw new ConflictException(LocalizedKeys.Courses.PrerequisiteSelfReference);

        _ = await _courses.GetByIdAsync(request.PrerequisiteCourseId, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.PrerequisiteNotFound);

        var current = await _courses.GetPrerequisiteRowsAsync(courseId, cancellationToken);
        if (current.Any(r => r.PrerequisiteCourseId == request.PrerequisiteCourseId))
            throw new ConflictException(LocalizedKeys.Courses.PrerequisiteAlreadyExists);

        var targetIds = current.Select(r => r.PrerequisiteCourseId).Append(request.PrerequisiteCourseId).ToList();
        await EnsureAcyclicAsync(courseId, targetIds, cancellationToken);

        await _courses.AddPrerequisiteAsync(
            new CoursePrerequisite { CourseId = courseId, PrerequisiteCourseId = request.PrerequisiteCourseId },
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid courseId, Guid prerequisiteCourseId, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(courseId, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);
        course.EnsureMutable();

        var current = await _courses.GetPrerequisiteRowsAsync(courseId, cancellationToken);
        var row = current.FirstOrDefault(r => r.PrerequisiteCourseId == prerequisiteCourseId)
            ?? throw new NotFoundException(LocalizedKeys.Courses.PrerequisiteNotFound);

        _courses.RemovePrerequisite(row);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// DAG guard. Builds the dependency adjacency from every edge in the
    /// catalog EXCEPT the course's own current edges (which the caller is
    /// replacing with <paramref name="proposedPrerequisiteIds"/>), then checks
    /// whether any proposed prerequisite can reach the course by following
    /// prerequisite chains. A hit means the proposal closes a cycle.
    /// </summary>
    private async Task EnsureAcyclicAsync(Guid courseId, IReadOnlyList<Guid> proposedPrerequisiteIds, CancellationToken cancellationToken)
    {
        if (proposedPrerequisiteIds.Count == 0) return;

        var allRows = await _courses.GetAllPrerequisiteRowsAsync(cancellationToken);
        var adjacency = new Dictionary<Guid, List<Guid>>();
        foreach (var row in allRows.Where(r => r.CourseId != courseId))
        {
            if (!adjacency.TryGetValue(row.CourseId, out var list))
            {
                list = new List<Guid>();
                adjacency[row.CourseId] = list;
            }
            list.Add(row.PrerequisiteCourseId);
        }

        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>(proposedPrerequisiteIds);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == courseId)
                throw new ConflictException(LocalizedKeys.Courses.PrerequisiteCycle);
            if (!visited.Add(node)) continue;
            if (adjacency.TryGetValue(node, out var next))
            {
                foreach (var n in next) stack.Push(n);
            }
        }
    }
}

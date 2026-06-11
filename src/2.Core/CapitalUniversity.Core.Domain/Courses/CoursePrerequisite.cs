using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Courses;

/// <summary>
/// Catalog-level prerequisite edge: <see cref="CourseId"/> requires
/// <see cref="PrerequisiteCourseId"/> to have been completed first. Pure
/// directed edge between two catalog courses — enrollment enforcement and
/// transcript checks belong to a future Registration module; this row only
/// describes the catalog dependency graph (kept acyclic by the service).
/// Both ends are FK-by-id with no navigation, per the catalog read-through rule.
/// </summary>
public class CoursePrerequisite : BaseEntity
{
    public Guid CourseId { get; set; }
    public Guid PrerequisiteCourseId { get; set; }
}

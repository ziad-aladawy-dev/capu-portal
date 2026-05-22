using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.Courses.Authorization;

/// <summary>
/// Declares the catalog-management permissions for the Courses module. Two
/// resources: <c>courses</c> (catalog) and <c>academic-plans</c> (curriculum
/// composition), each grantable independently of the other. Prerequisites,
/// registration and transcript permissions belong to a future Registration
/// module and are deliberately absent here per <c>docs/Plan.md</c>.
/// </summary>
public sealed class CoursesPermissionManifest : IPermissionManifest
{
    public string Module => "courses";
    public string DisplayName => "Course Catalog";
    public string? Icon => "BookOpen";
    public int? OrderNumber => 7;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("courses",        "Course Catalog",  0),
        ResourceDefinition.WithCrudActions("academic-plans", "Academic Plans",  1),
    };
}

using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

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
    public string DisplayName => LocalizedJson.Of("كتالوج المقررات", "Course Catalog");
    public string? Icon => "BookOpen";
    public int? OrderNumber => 7;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("courses",        LocalizedJson.Of("كتالوج المقررات", "Course Catalog"), 0),
        ResourceDefinition.WithCrudActions("academic-plans", LocalizedJson.Of("الخطط الدراسية",  "Academic Plans"), 1),
    };
}

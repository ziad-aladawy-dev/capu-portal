using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Declares the catalog-management permissions for the Courses module. The
/// manifest synchroniser materialises one <c>Service</c> row from the shared
/// DisplayName, so granting any of these to a role lights up the whole catalog
/// surface (list / read / mutate). Prerequisites, registration and transcript
/// permissions belong to a future Registration module and are deliberately
/// absent here per <c>docs/Plan.md</c>.
/// </summary>
public sealed class CoursesPermissionManifest : IPermissionManifest
{
    public string Module => "courses";
    public string DisplayName => "Course Catalog";
    public string? Icon => "BookOpen";
    public int? OrderNumber => 7;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create("courses", "View",      "Course Catalog", 0),
        PermissionDefinition.Create("courses", "Insert",    "Course Catalog", 0),
        PermissionDefinition.Create("courses", "EditClose", "Course Catalog", 0),
        PermissionDefinition.Create("courses", "Open",      "Course Catalog", 0),
        PermissionDefinition.Create("courses", "Delete",    "Course Catalog", 0),
        // Academic-plan composition (curriculum layout). Lives under the same
        // module as the catalog but uses its own Service row so it can be
        // granted independently of catalog-edit rights.
        PermissionDefinition.Create("academic-plans", "View",      "Academic Plans", 1),
        PermissionDefinition.Create("academic-plans", "Insert",    "Academic Plans", 1),
        PermissionDefinition.Create("academic-plans", "EditClose", "Academic Plans", 1),
        PermissionDefinition.Create("academic-plans", "Open",      "Academic Plans", 1),
        PermissionDefinition.Create("academic-plans", "Delete",    "Academic Plans", 1),
    };
}

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
    private const string ResourceCourses = "courses";
    private const string DisplayCourseCatalog = "Course Catalog";
    private const string ResourceAcademicPlans = "academic-plans";
    private const string DisplayAcademicPlans = "Academic Plans";

    public string Module => ResourceCourses;
    public string DisplayName => DisplayCourseCatalog;
    public string? Icon => "BookOpen";
    public int? OrderNumber => 7;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create(ResourceCourses, "View",      DisplayCourseCatalog, 0),
        PermissionDefinition.Create(ResourceCourses, "Insert",    DisplayCourseCatalog, 0),
        PermissionDefinition.Create(ResourceCourses, "EditClose", DisplayCourseCatalog, 0),
        PermissionDefinition.Create(ResourceCourses, "Open",      DisplayCourseCatalog, 0),
        PermissionDefinition.Create(ResourceCourses, "Delete",    DisplayCourseCatalog, 0),
        // Academic-plan composition (curriculum layout). Lives under the same
        // module as the catalog but uses its own Service row so it can be
        // granted independently of catalog-edit rights.
        PermissionDefinition.Create(ResourceAcademicPlans, "View",      DisplayAcademicPlans, 1),
        PermissionDefinition.Create(ResourceAcademicPlans, "Insert",    DisplayAcademicPlans, 1),
        PermissionDefinition.Create(ResourceAcademicPlans, "EditClose", DisplayAcademicPlans, 1),
        PermissionDefinition.Create(ResourceAcademicPlans, "Open",      DisplayAcademicPlans, 1),
        PermissionDefinition.Create(ResourceAcademicPlans, "Delete",    DisplayAcademicPlans, 1),
    };
}

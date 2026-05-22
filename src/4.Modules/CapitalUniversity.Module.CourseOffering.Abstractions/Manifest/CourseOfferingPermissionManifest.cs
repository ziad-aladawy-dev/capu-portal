using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Modules.CourseOffering.Abstractions.Manifest;

/// <summary>
/// Declares the CourseOffering module's permission surface. One resource —
/// the offering itself. Granting <c>Insert</c> / <c>EditClose</c> hands an
/// operator control over runtime availability for a (term, node) target;
/// registration verbs belong to a future Registration module manifest.
/// </summary>
public sealed class CourseOfferingPermissionManifest : IPermissionManifest
{
    public string Module => "course-offerings";
    public string DisplayName => "Course Offerings";
    public string? Icon => "CalendarCheck";
    public int? OrderNumber => 10;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("course-offerings", "Course Offerings", 0),
    };
}

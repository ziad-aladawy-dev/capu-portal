using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

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
    public string DisplayName => LocalizedJson.Of("طرح المقررات", "Course Offerings");
    public string? Icon => "CalendarCheck";
    public int? OrderNumber => 10;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions(
            "course-offerings",
            LocalizedJson.Of("طرح المقررات", "Course Offerings"),
            0),
    };
}

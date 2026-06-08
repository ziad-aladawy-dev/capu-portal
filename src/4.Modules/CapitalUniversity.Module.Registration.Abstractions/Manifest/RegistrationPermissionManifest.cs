using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Modules.Registration.Abstractions.Manifest;

/// <summary>
/// Declares the Registration module's permission surface. One resource — the
/// student's registered courses. Read-heavy: in practice only <c>View</c> is
/// exercised by the endpoints, but the canonical CRUD quintet is declared so
/// the permission tree stays uniform with every other module. Row-level access
/// (a student sees only their own registrations) is enforced in the service
/// layer through <c>IEffectiveScope</c>, not by a coarser action grant.
/// </summary>
public sealed class RegistrationPermissionManifest : IPermissionManifest
{
    public string Module => "registered-courses";
    public string DisplayName => LocalizedJson.Of("المقررات المسجلة", "Registered Courses");
    public string? Icon => "BookOpenCheck";
    public int? OrderNumber => 11;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions(
            "registered-courses",
            LocalizedJson.Of("المقررات المسجلة", "Registered Courses"),
            0),
    };
}

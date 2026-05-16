using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Owns the consolidated academic-timeline permissions — a single resource gating
/// both Academic Years and Semesters. The system's <see cref="PermissionIdentity.ResourceFor"/>
/// already collapses every academics-module service down to the
/// <c>academic-years</c> resource at lookup time, so declaring two parallel
/// resources here would have produced equal canonical names anyway. Combining
/// also matches how operators think about the role: anyone with academic temporal
/// scope management needs both tables, not one.
///
/// <para>
/// All five PermissionDefinitions share the <c>"Academic Timeline"</c> DisplayName
/// so the synchroniser creates a single Service row — one DB row, one role grant
/// per role, both controllers gated by it.
/// </para>
/// </summary>
public sealed class AcademicsPermissionManifest : IPermissionManifest
{
    public string Module => "academics";
    public string DisplayName => "Academic Timeline";
    public string? Icon => "Calendar";
    public int? OrderNumber => 6;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create("academic-years", "View",      "Academic Timeline", 0),
        PermissionDefinition.Create("academic-years", "Insert",    "Academic Timeline", 0),
        PermissionDefinition.Create("academic-years", "EditClose", "Academic Timeline", 0),
        PermissionDefinition.Create("academic-years", "Open",      "Academic Timeline", 0),
        PermissionDefinition.Create("academic-years", "Delete",    "Academic Timeline", 0),
    };
}

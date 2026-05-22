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
    private const string ResourceAcademicYears = "academic-years";
    private const string DisplayAcademicTimeline = "Academic Timeline";

    public string Module => "academics";
    public string DisplayName => DisplayAcademicTimeline;
    public string? Icon => "Calendar";
    public int? OrderNumber => 6;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create(ResourceAcademicYears, "View",      DisplayAcademicTimeline, 0),
        PermissionDefinition.Create(ResourceAcademicYears, "Insert",    DisplayAcademicTimeline, 0),
        PermissionDefinition.Create(ResourceAcademicYears, "EditClose", DisplayAcademicTimeline, 0),
        PermissionDefinition.Create(ResourceAcademicYears, "Open",      DisplayAcademicTimeline, 0),
        PermissionDefinition.Create(ResourceAcademicYears, "Delete",    DisplayAcademicTimeline, 0),
    };
}

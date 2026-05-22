using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.Semesters.Authorization;

/// <summary>
/// Owns the consolidated academic-timeline permissions — a single resource
/// (<c>academic-years</c>) gating both Academic Years and Semesters. Combining
/// matches how operators think about the role: anyone with academic temporal
/// scope management needs both tables, not one.
/// </summary>
public sealed class AcademicsPermissionManifest : IPermissionManifest
{
    public string Module => "academics";
    public string DisplayName => "Academic Timeline";
    public string? Icon => "Calendar";
    public int? OrderNumber => 6;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("academic-years", "Academic Timeline", 0),
    };
}

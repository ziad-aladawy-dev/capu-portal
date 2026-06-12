using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Core.Application.StaffManagement.Authorization;

/// <summary>
/// Declares the Staff Management module's permission surface.
/// </summary>
public sealed class StaffPermissionManifest : IPermissionManifest
{
    public string Module => "staff";
    public string DisplayName => LocalizedJson.Of("إدارة الموظفين", "Staff Management");
    public string? Icon => "Briefcase";
    public int? OrderNumber => 8;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("staff", LocalizedJson.Of("الموظفون", "Staff"), 0),
    };
}
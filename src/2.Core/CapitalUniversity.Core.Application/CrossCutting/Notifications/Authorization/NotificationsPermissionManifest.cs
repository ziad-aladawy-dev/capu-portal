using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Owns the <c>notifications</c> module — notifications infrastructure permissions.
/// Mirrors the existing seeder rows so the synchroniser converges to no-op.
/// </summary>
public sealed class NotificationsPermissionManifest : IPermissionManifest
{
    public string Module => "notifications";
    public string DisplayName => "Notifications";
    public string? Icon => "Bell";
    public int? OrderNumber => 7;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create("notifications", "View",   "View Notifications", 0),
        PermissionDefinition.Create("notifications", "Insert", "Send Notifications", 1),
    };
}

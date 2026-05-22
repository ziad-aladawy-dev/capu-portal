using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Core.Application.CrossCutting.Notifications.Authorization;

/// <summary>
/// Owns the <c>notifications</c> module — one resource (<c>notifications</c>)
/// with View + Insert actions ("send" maps to Insert in the canonical verb set).
/// </summary>
public sealed class NotificationsPermissionManifest : IPermissionManifest
{
    public string Module => "notifications";
    public string DisplayName => LocalizedJson.Of("الإشعارات", "Notifications");
    public string? Icon => "Bell";
    public int? OrderNumber => 7;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        new ResourceDefinition
        {
            Key = "notifications",
            DisplayName = LocalizedJson.Of("الإشعارات", "Notifications"),
            OrderNumber = 0,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("View",   0),
                ActionDefinition.Hierarchical("Insert", 1, "View"),
            },
        },
    };
}

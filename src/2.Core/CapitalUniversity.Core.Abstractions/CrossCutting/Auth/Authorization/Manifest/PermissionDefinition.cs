namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// One declared permission inside a module's manifest. The pair <c>(Resource, Action)</c>
/// is unique inside a manifest; the canonical name is <c>{Module}.{Resource}.{Action}</c>
/// once paired with the owning <see cref="IPermissionManifest.Module"/> key.
/// </summary>
public sealed record PermissionDefinition
{
    /// <summary>Resource inside the module (e.g., <c>"academic-years"</c>, <c>"semesters"</c>, <c>"permissions"</c>).</summary>
    public string Resource { get; init; } = string.Empty;

    /// <summary>Action verb (e.g., <c>"View"</c>, <c>"Insert"</c>, <c>"EditClose"</c>, <c>"Open"</c>, <c>"Delete"</c>).</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>UI display name for the underlying <c>Service</c> row.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Ordering inside the module's UI listing. Lower = earlier.</summary>
    public int OrderNumber { get; init; }

    public static PermissionDefinition Create(string resource, string action, string displayName, int orderNumber = 0) =>
        new() { Resource = resource, Action = action, DisplayName = displayName, OrderNumber = orderNumber };

    /// <summary>Returns <c>{module}.{Resource}.{Action}</c> — the canonical permission name.</summary>
    public string CanonicalName(string module) => $"{module}.{Resource}.{Action}";
}

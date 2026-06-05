using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

// Admin / navigation surfaces (dashboard, users, structure, programs) are single-
// resource modules gating menu visibility and the corresponding admin screens.
// They were previously seeded as Module/Resource rows with no manifest, which forced
// the seeder to expand grants via a hardcoded CRUD ladder. Declaring them here makes
// the manifest the single source of truth for their action graphs too.

/// <summary>Dashboard landing surface. Module = <c>dashboard</c>, Resource = <c>dashboard</c>.</summary>
public sealed class DashboardPermissionManifest : IPermissionManifest
{
    public string Module => "dashboard";
    public string DisplayName => LocalizedJson.Of("لوحة المعلومات", "Dashboard");
    public string? Icon => "LayoutDashboard";
    public int? OrderNumber => 0;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("dashboard", LocalizedJson.Of("لوحة المعلومات", "Dashboard"), 0),
    };
}

/// <summary>User management surface. Module = <c>users</c>, Resource = <c>users</c>.</summary>
public sealed class UsersPermissionManifest : IPermissionManifest
{
    public string Module => "users";
    public string DisplayName => LocalizedJson.Of("إدارة المستخدمين", "User Management");
    public string? Icon => "Users";
    public int? OrderNumber => 1;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("users", LocalizedJson.Of("المستخدمون", "Users"), 0),
    };
}

/// <summary>University structure surface. Module = <c>structure</c>, Resource = <c>structure</c>.</summary>
public sealed class StructurePermissionManifest : IPermissionManifest
{
    public string Module => "structure";
    public string DisplayName => LocalizedJson.Of("الهيكل الجامعي", "University Structure");
    public string? Icon => "Building2";
    public int? OrderNumber => 2;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("structure", LocalizedJson.Of("الهيكل", "Structure"), 0),
    };
}

/// <summary>Academic programs surface. Module = <c>programs</c>, Resource = <c>programs</c>.</summary>
public sealed class ProgramsPermissionManifest : IPermissionManifest
{
    public string Module => "programs";
    public string DisplayName => LocalizedJson.Of("البرامج الأكاديمية", "Academic Programs");
    public string? Icon => "BookOpen";
    public int? OrderNumber => 3;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("programs", LocalizedJson.Of("البرامج", "Programs"), 0),
    };
}

/// <summary>SIS integration surface. Module = <c>sync</c>, Resource = <c>sync</c>.
/// Granted to Super Admin via the seeder's "every resource" loop.</summary>
public sealed class SyncPermissionManifest : IPermissionManifest
{
    public string Module => "sync";
    public string DisplayName => LocalizedJson.Of("تكامل النظام", "SIS Integration");
    public string? Icon => "RefreshCw";
    public int? OrderNumber => 5;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("sync", LocalizedJson.Of("مزامنة النظام", "SIS Sync"), 0),
    };
}

/// <summary>System administration surface — currently the audit-log viewer.
/// Module = <c>system</c>, Resource = <c>audit-logs</c>. Only <c>View</c> maps to
/// an endpoint, but the resource declares the full CRUD action graph (matching
/// the other admin-nav resources) so the seeder's "grant every resource at
/// Delete" loop expands down to View for Super Admin.</summary>
public sealed class SystemPermissionManifest : IPermissionManifest
{
    public string Module => "system";
    public string DisplayName => LocalizedJson.Of("النظام", "System");
    public string? Icon => "ShieldCheck";
    public int? OrderNumber => 9;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("audit-logs", LocalizedJson.Of("سجل التدقيق", "Audit Log"), 0),
    };
}

using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Modules.StudentServices.Abstractions.Manifest;

/// <summary>
/// Declares the Student Services module's permission surface. Three resources:
///   <list type="bullet">
///     <item><c>services</c> — manage the service catalog (admin only).</item>
///     <item><c>requests</c> — process student requests (staff side).</item>
///     <item><c>workflows</c> — configure workflows (admin only).</item>
///   </list>
/// Students see and submit their own requests through the request resource;
/// the row-level scope check inside the service layer enforces own-data-only.
/// </summary>
public sealed class StudentServicesPermissionManifest : IPermissionManifest
{
    public string Module => "student-services";
    public string DisplayName => LocalizedJson.Of("خدمات الطلاب", "Student Services");
    public string? Icon => "FileText";
    public int? OrderNumber => 12;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions(
            "services",
            LocalizedJson.Of("الخدمات", "Services"),
            0),
        ResourceDefinition.WithCrudActions(
            "requests",
            LocalizedJson.Of("الطلبات", "Requests"),
            1),
        ResourceDefinition.WithCrudActions(
            "workflows",
            LocalizedJson.Of("سير العمل", "Workflows"),
            2),
    };
}

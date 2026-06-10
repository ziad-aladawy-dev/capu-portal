using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Module.StudentServices.Authorization;

public sealed class StudentServicesPermissionManifest : IPermissionManifest
{
    public string Module => "student-services";
    public string DisplayName => LocalizedJson.Of("خدمات الطلاب", "Student Services");
    public string? Icon => "Package";
    public int? OrderNumber => 12;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("services", LocalizedJson.Of("الخدمات", "Services"), 0),
        ResourceDefinition.WithCrudActions("requests", LocalizedJson.Of("الطلبات", "Requests"), 1),
        ResourceDefinition.WithCrudActions("workflows", LocalizedJson.Of("سير العمل", "Workflows"), 2),
    };
}

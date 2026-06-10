using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Modules.StudentServices.Abstractions.Manifest;

public class StudentServicesPermissionManifest : IPermissionManifest
{
    public string Module => "student-services";

    public string DisplayName => LocalizedJson.Of("الخدمات الطلابية", "Student Services");
    
    public string? Icon => "UserGroup";
    
    public int? OrderNumber => 10;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new List<ResourceDefinition>
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
            LocalizedJson.Of("مسارات العمل", "Workflows"),
            2)
    };
}

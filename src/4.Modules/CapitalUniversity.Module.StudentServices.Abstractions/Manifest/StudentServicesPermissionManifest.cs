using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Manifest;

public sealed class StudentServicesPermissionManifest : IPermissionManifest
{
    public string Module => "student-services";
    public string DisplayName => LocalizedJson.Of("خدمات الطلاب", "Student Services");
    public string? Icon => "Briefcase";
    public int? OrderNumber => 12;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        new ResourceDefinition
        {
            Key = "services",
            DisplayName = LocalizedJson.Of("الخدمات", "Services"),
            OrderNumber = 0,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("View",      0),
                ActionDefinition.Hierarchical("Insert",    1, "View"),
                ActionDefinition.Hierarchical("EditClose", 2, "View", "Insert"),
                ActionDefinition.Hierarchical("Open",      3, "View", "Insert", "EditClose"),
                ActionDefinition.Hierarchical("Delete",    4, "View", "Insert", "EditClose", "Open"),
            }
        },
        new ResourceDefinition
        {
            Key = "requests",
            DisplayName = LocalizedJson.Of("طلبات الخدمات", "Service Requests"),
            OrderNumber = 1,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("View",      0),
                ActionDefinition.Hierarchical("Insert",    1, "View"),
                ActionDefinition.Hierarchical("EditClose", 2, "View", "Insert"),
                ActionDefinition.Hierarchical("Open",      3, "View", "Insert", "EditClose"),
                ActionDefinition.Hierarchical("Delete",    4, "View", "Insert", "EditClose", "Open"),
                ActionDefinition.Explicit("Assign", 5, dangerous: false),
            }
        },
        new ResourceDefinition
        {
            Key = "workflows",
            DisplayName = LocalizedJson.Of("سير العمل", "Workflows"),
            OrderNumber = 2,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("View",      0),
                ActionDefinition.Hierarchical("Insert",    1, "View"),
                ActionDefinition.Hierarchical("EditClose", 2, "View", "Insert"),
                ActionDefinition.Hierarchical("Open",      3, "View", "Insert", "EditClose"),
                ActionDefinition.Hierarchical("Delete",    4, "View", "Insert", "EditClose", "Open"),
            }
        },
    };
}
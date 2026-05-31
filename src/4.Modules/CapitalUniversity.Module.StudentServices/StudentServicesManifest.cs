using CapitalUniversity.Core.Abstractions.CrossCutting.Modules;

namespace CapitalUniversity.Module.StudentServices;

public class StudentServicesManifest : IManifest
{
    public string Id => "CapitalUniversity.Module.StudentServices";
    public string Name => "Student Services Module";
    public string Version => "1.0.0";
    public string CoreVersionRequired => "1.0.0";

    public IEnumerable<ModuleDependency> DependsOn => new List<ModuleDependency>();

    public BackendConfig Backend => new BackendConfig
    {
        Assembly = "CapitalUniversity.Module.StudentServices",
        Schema = "StudentServices",
        MigrationsFolder = "Infrastructure/Persistence/Migrations"
    };

    public FrontendConfig Frontend => new FrontendConfig
    {
        RemoteUrl = "/_content/CapitalUniversity.Module.StudentServices/module.js",
        RemoteName = "StudentServicesModule"
    };

    public IEnumerable<string> Permissions => new[]
    {
        "student-services.services.View",
        "student-services.services.Insert",
        "student-services.services.EditClose",
        "student-services.services.Delete",
        "student-services.workflows.View",
        "student-services.workflows.Insert",
        "student-services.workflows.EditClose",
        "student-services.workflows.Delete",
        "student-services.requests.ViewAssigned",
        "student-services.requests.Assign",
        "student-services.requests.Manage"
    };

    public IEnumerable<MenuItem> MenuItems => new[]
    {
        new MenuItem
        {
            Portal = "staff",
            Section = "services",
            Title = "Manage Services",
            Icon = "fas fa-cogs",
            Route = "/staff/student-services/services",
            RequiredPermission = "student-services.services.View",
            Level = 1
        },
        new MenuItem
        {
            Portal = "staff",
            Section = "services",
            Title = "Student Requests",
            Icon = "fas fa-tasks",
            Route = "/staff/student-services/requests",
            RequiredPermission = "student-services.requests.ViewAssigned",
            Level = 2
        },
        new MenuItem
        {
            Portal = "student",
            Section = "services",
            Title = "Available Services",
            Icon = "fas fa-list",
            Route = "/student/services",
            RequiredPermission = null,
            Level = 1
        },
        new MenuItem
        {
            Portal = "student",
            Section = "services",
            Title = "My Requests",
            Icon = "fas fa-history",
            Route = "/student/requests",
            RequiredPermission = null,
            Level = 2
        }
    };

    public EventConfig Events => new EventConfig
    {
        Publishes = new[] { "student_request_submitted", "student_request_status_changed" },
        Subscribes = new List<string>()
    };

    public string SettingsSchema => @"{
      ""type"": ""object"",
      ""properties"": {
        ""MaxAttachmentsPerRequest"": { ""type"": ""integer"", ""default"": 10 },
        ""AllowedFileTypes"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""default"": [""pdf"",""jpg"",""png""] }
      }
    }";
}
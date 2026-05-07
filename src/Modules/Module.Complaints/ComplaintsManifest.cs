using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.Modules;

namespace CapitalUniversity.Module.Complaints;

public class ComplaintsManifest : IManifest
{
    public string Id => "complaints";
    public string Name => "Student Complaints";
    public string Version => "1.0.0";
    public string CoreVersionRequired => ">=1.0.0";

    public IEnumerable<ModuleDependency> DependsOn => new[]
    {
        new ModuleDependency { Id = "students", Version = ">=1.0.0" }
    };

    public BackendConfig Backend => new BackendConfig
    {
        Assembly = "CapitalUniversity.Module.Complaints.dll",
        Schema = "complaints",
        MigrationsFolder = "Migrations"
    };

    public FrontendConfig Frontend => new FrontendConfig
    {
        RemoteUrl = "/modules/complaints/remoteEntry.js",
        RemoteName = "complaints"
    };

    public IEnumerable<string> Permissions => new[]
    {
        "complaints.view",
        "complaints.submit",
        "complaints.manage"
    };

    public IEnumerable<MenuItem> MenuItems => new[]
    {
        new MenuItem
        {
            Portal = "student",
            Section = "Support",
            Title = "Complaints",
            Icon = "message-circle",
            Route = "/complaints/my",
            Level = 1
        },
        new MenuItem
        {
            Portal = "admin",
            Section = "Support",
            Title = "Manage Complaints",
            Icon = "message-square",
            Route = "/complaints/manage",
            RequiredPermission = "complaints.manage",
            Level = 1
        }
    };

    public EventConfig Events => new EventConfig
    {
        Publishes = new[] { "complaints.submitted", "complaints.resolved" },
        Subscribes = new[] { "student.profile_updated" }
    };

    public string SettingsSchema => "settings.schema.json";
}

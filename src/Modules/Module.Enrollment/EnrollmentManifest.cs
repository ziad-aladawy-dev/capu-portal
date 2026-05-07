using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.Modules;

namespace CapitalUniversity.Module.Enrollment;

public class EnrollmentManifest : IManifest
{
    public string Id => "enrollment";
    public string Name => "Course Enrollment";
    public string Version => "1.0.0";
    public string CoreVersionRequired => ">=1.0.0";

    public IEnumerable<ModuleDependency> DependsOn => new[]
    {
        new ModuleDependency { Id = "students", Version = ">=1.0.0" }
    };

    public BackendConfig Backend => new BackendConfig
    {
        Assembly = "CapitalUniversity.Module.Enrollment.dll",
        Schema = "enrollment",
        MigrationsFolder = "Migrations"
    };

    public FrontendConfig Frontend => new FrontendConfig
    {
        RemoteUrl = "/modules/enrollment/remoteEntry.js",
        RemoteName = "enrollment"
    };

    public IEnumerable<string> Permissions => new[]
    {
        "enrollment.courses.view",
        "enrollment.courses.register",
        "enrollment.courses.manage"
    };

    public IEnumerable<MenuItem> MenuItems => new[]
    {
        new MenuItem
        {
            Portal = "student",
            Section = "Academics",
            Title = "Registration",
            Icon = "calendar",
            Route = "/enrollment/register",
            Level = 1
        }
    };

    public EventConfig Events => new EventConfig
    {
        Publishes = new[] { "enrollment.course_registered", "enrollment.course_dropped" },
        Subscribes = new[] { "registration.semester_started" }
    };

    public string SettingsSchema => "settings.schema.json";
}

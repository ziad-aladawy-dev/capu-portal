using System.Collections.Generic;

namespace CapitalUniversity.Core.Abstractions.Modules;

public interface IManifest
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string CoreVersionRequired { get; }
    
    IEnumerable<ModuleDependency> DependsOn { get; }
    BackendConfig Backend { get; }
    FrontendConfig Frontend { get; }
    
    IEnumerable<string> Permissions { get; }
    IEnumerable<MenuItem> MenuItems { get; }
    EventConfig Events { get; }
    
    string SettingsSchema { get; }
}

public class ModuleDependency
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

public class BackendConfig
{
    public string Assembly { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string MigrationsFolder { get; set; } = string.Empty;
}

public class FrontendConfig
{
    public string RemoteUrl { get; set; } = string.Empty;
    public string RemoteName { get; set; } = string.Empty;
}

public class MenuItem
{
    public string Portal { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string? RequiredPermission { get; set; }
    public int? Level { get; set; }
}

public class EventConfig
{
    public IEnumerable<string> Publishes { get; set; } = new List<string>();
    public IEnumerable<string> Subscribes { get; set; } = new List<string>();
}

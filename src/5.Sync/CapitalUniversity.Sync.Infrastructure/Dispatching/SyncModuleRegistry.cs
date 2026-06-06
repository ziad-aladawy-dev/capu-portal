using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Contracts;

namespace CapitalUniversity.Sync.Infrastructure.Dispatching;

public sealed class SyncModuleRegistry : ISyncModuleRegistry
{
    private readonly IReadOnlyDictionary<string, ISyncModule> _modules;

    public SyncModuleRegistry(IEnumerable<ISyncModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        _modules = modules.ToDictionary(
            keySelector: m => m.ModuleName,
            elementSelector: m => m,
            comparer: StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> RegisteredModules => _modules.Keys.ToArray();

    public ISyncModule Resolve(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("Module name must be provided.", nameof(moduleName));
        }

        if (!_modules.TryGetValue(moduleName, out var module))
        {
            throw new SyncModuleNotFoundException(moduleName);
        }

        return module;
    }

    public bool TryResolve(string moduleName, out ISyncModule? module)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            module = null;
            return false;
        }

        return _modules.TryGetValue(moduleName, out module);
    }
}
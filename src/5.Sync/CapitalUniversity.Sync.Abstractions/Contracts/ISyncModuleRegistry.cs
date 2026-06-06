namespace CapitalUniversity.Sync.Abstractions.Contracts;

public interface ISyncModuleRegistry
{
    IReadOnlyCollection<string> RegisteredModules { get; }

    ISyncModule Resolve(string moduleName);

    bool TryResolve(string moduleName, out ISyncModule? module);
}
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Infrastructure.Execution;

/// <summary>
/// <c>AsyncLocal</c>-backed implementation of <see cref="ISyncRunContextAccessor"/>.
/// Registered as a singleton; the per-flow value is what makes concurrent runs on
/// different worker threads see their own context. <see cref="SyncModuleExecutor"/>
/// calls <see cref="Set"/> immediately after building the run's
/// <see cref="SyncContext"/>, before invoking the module — the value then flows
/// into the pipeline's child scope and into any writer resolved there.
/// </summary>
public sealed class SyncRunContextAccessor : ISyncRunContextAccessor
{
    private static readonly AsyncLocal<SyncContext?> Storage = new();

    public SyncContext? Current => Storage.Value;

    /// <summary>Pins the context for the current async flow (and everything it awaits).</summary>
    public void Set(SyncContext? context) => Storage.Value = context;
}

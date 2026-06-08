using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Ambient access to the <see cref="SyncContext"/> of the run currently executing
/// on this async flow. Set once by the executor when a run starts; read by
/// scope-resolved pipeline components (e.g. a writer that needs the run's
/// <c>CorrelationId</c> / <c>Attempt</c> to dead-letter an unresolvable row).
///
/// <para>
/// Backed by <c>AsyncLocal</c>, so the value flows into the child DI scope and
/// task tree the pull/push pipeline creates without threading it through every
/// signature. <see cref="Current"/> is <c>null</c> outside a run (e.g. a writer
/// exercised directly in a unit test that didn't set it).
/// </para>
/// </summary>
public interface ISyncRunContextAccessor
{
    SyncContext? Current { get; }
}

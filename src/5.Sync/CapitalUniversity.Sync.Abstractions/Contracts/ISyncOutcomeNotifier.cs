using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Fans out an in-app notification to everyone who can access the sync layer
/// (every holder of the <c>sync</c> permission) when a run reaches a terminal
/// outcome — success or terminal failure (dead-letter).
///
/// <para>
/// Implemented host-side (<c>CapitalUniversity.Sync.Host</c>) where
/// <c>CoreDbContext</c> is available; the executor and dead-letter filter live in
/// <c>Sync.Infrastructure</c> and depend only on this abstraction so they never
/// reference Core. Resolution is therefore optional — a host that does not
/// register an implementation simply skips notification.
/// </para>
///
/// <para>
/// Best-effort: the run's audit state is already committed by the time this is
/// invoked, so implementations MUST NOT throw back into the caller. Callers
/// invoke it on a path that swallows and logs failures.
/// </para>
/// </summary>
public interface ISyncOutcomeNotifier
{
    Task NotifyAsync(SyncOutcomeNotice notice, CancellationToken cancellationToken);
}

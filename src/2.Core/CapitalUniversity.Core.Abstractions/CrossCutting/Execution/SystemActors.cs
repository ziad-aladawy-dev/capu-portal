namespace CapitalUniversity.Core.Abstractions.CrossCutting.Execution;

/// <summary>
/// Canonical, stable identifiers for non-user actors that mutate state from
/// background processes (outbox dispatch, scheduled jobs, system reconciliation).
///
/// <para>
/// Runtime Hardening Plan §3.2: background jobs must not fabricate an
/// authenticated user. Code that previously fell back to <c>Guid.Empty</c> or
/// the resolved-as-zero <see cref="ICurrentUser.Id"/> should reach for one of
/// these constants instead so audit logs carry a deterministic, recognisable
/// actor identity even when no <see cref="HttpContext"/> exists.
/// </para>
///
/// <para>
/// IDs are fixed at compile-time on purpose — they're not stored in the users
/// table, so changing them later would orphan historical audit rows. Treat them
/// as constants the way you would a magic enum value.
/// </para>
/// </summary>
public static class SystemActors
{
    /// <summary>
    /// Generic background-processor actor — outbox dispatcher, fee jobs, etc.
    /// All deterministic so audit queries can filter on them.
    /// </summary>
    public static readonly Guid BackgroundProcessor = new("00000000-0000-0000-0000-00000000B6C5");

    /// <summary>
    /// Academic timeline reconciliation (current-year / current-semester resolver).
    /// </summary>
    public static readonly Guid AcademicTimeline = new("00000000-0000-0000-0000-00000000A7E1");

    /// <summary>
    /// Outbox dispatch loop specifically — distinguishes outbox-driven mutations
    /// from generic background work.
    /// </summary>
    public static readonly Guid OutboxDispatcher = new("00000000-0000-0000-0000-00000000017B");

    /// <summary>
    /// Default name written to logs alongside the actor ID so operators can read
    /// a log line without cross-referencing this file.
    /// </summary>
    public const string DisplayName = "System";
}

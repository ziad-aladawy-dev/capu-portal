namespace CapitalUniversity.Sync.Abstractions.Enums;

/// <summary>
/// Lifecycle status for an outbox row used by Internal → External push flows.
/// Identical int values across all modules; lifted here to remove per-module
/// enum duplication. Modules persist this as a backing int column so existing
/// migrations (which stored the per-module enum as int) keep working unchanged.
/// </summary>
/// <remarks>
/// Pending → Processed on a successful push; Pending stays Pending across
/// transient failures so the next push tick re-attempts. Failed is reserved
/// for rows whose AttemptCount crosses the writer's poison-row cap.
/// </remarks>
public enum OutboxStatus
{
    Pending = 0,
    Processed = 1,
    Failed = 2
}
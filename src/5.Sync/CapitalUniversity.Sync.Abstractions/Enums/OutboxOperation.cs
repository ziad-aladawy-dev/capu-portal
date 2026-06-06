namespace CapitalUniversity.Sync.Abstractions.Enums;

/// <summary>
/// Logical action carried by an outbox row. Lifted here so modules share the
/// same enum values rather than each declaring an identical local copy.
/// Storage as int via EF keeps the column compact and forward-compatible.
/// </summary>
public enum OutboxOperation
{
    Upsert = 0,
    Delete = 1
}
namespace CapitalUniversity.Sync.Abstractions.Enums;

public enum SyncRunStatus
{
    Enqueued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    DeadLettered = 4,
    Cancelled = 5
}
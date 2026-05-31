namespace CapitalUniversity.Sync.Persistence.Entities;

public sealed class SyncFailureEntity
{
    public long Id { get; set; }
    public Guid CorrelationId { get; set; }
    public string? HangfireJobId { get; set; }
    public int Attempt { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}
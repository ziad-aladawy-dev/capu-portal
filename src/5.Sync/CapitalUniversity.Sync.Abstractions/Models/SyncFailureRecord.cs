namespace CapitalUniversity.Sync.Abstractions.Models;

public sealed class SyncFailureRecord
{
    public required Guid CorrelationId { get; init; }

    public string? HangfireJobId { get; init; }

    public required int Attempt { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required string ErrorType { get; init; }

    public required string ErrorMessage { get; init; }

    public string? StackTrace { get; init; }
}
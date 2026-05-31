using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Abstractions.Models;

public sealed class SyncDeadLetterRecord
{
    public required Guid CorrelationId { get; init; }

    public required string HangfireJobId { get; init; }

    public required string ModuleName { get; init; }

    public required SyncDirection Direction { get; init; }

    public required int AttemptedCount { get; init; }

    public DateTimeOffset TerminalAt { get; init; } = DateTimeOffset.UtcNow;

    public string? LastError { get; init; }
}
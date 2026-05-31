namespace CapitalUniversity.Sync.Abstractions.Models;

public sealed class SyncResult
{
    public required bool Success { get; init; }

    public int RecordsProcessed { get; init; }

    public int RecordsFailed { get; init; }

    public TimeSpan Duration { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static SyncResult Ok(int processed, TimeSpan duration, IReadOnlyList<string>? warnings = null)
        => new()
        {
            Success = true,
            RecordsProcessed = processed,
            Duration = duration,
            Warnings = warnings ?? Array.Empty<string>()
        };

    public static SyncResult Failed(string errorMessage, TimeSpan duration, int processed = 0, int failed = 0)
        => new()
        {
            Success = false,
            RecordsProcessed = processed,
            RecordsFailed = failed,
            Duration = duration,
            ErrorMessage = errorMessage
        };
}
using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Abstractions.Models;

/// <summary>
/// Terminal outcome of a sync run, handed to <c>ISyncOutcomeNotifier</c> so the
/// people who operate the sync layer learn that a run finished or failed without
/// having to watch the dashboard. Carries only what a notification needs — not
/// the full audit record.
/// </summary>
/// <param name="CorrelationId">The run's correlation id (for log/trace stitching).</param>
/// <param name="ModuleName">Sync module that ran (e.g. "Student", "Finance").</param>
/// <param name="Direction">Pull or Push.</param>
/// <param name="Success"><c>true</c> on a successful completion, <c>false</c> on terminal failure (dead-letter).</param>
/// <param name="RecordsProcessed">Records processed (success path; 0 on failure).</param>
/// <param name="RecordsFailed">Records that failed within an otherwise-successful run.</param>
/// <param name="Error">Terminal error message on failure; <c>null</c> on success.</param>
public sealed record SyncOutcomeNotice(
    Guid CorrelationId,
    string ModuleName,
    SyncDirection Direction,
    bool Success,
    int RecordsProcessed,
    int RecordsFailed,
    string? Error);

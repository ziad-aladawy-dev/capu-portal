using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Student.Configuration;

/// <summary>
/// Fail-fast validator for <see cref="StudentSyncOptions"/>.
///
/// Caps <see cref="StudentSyncOptions.BatchSize"/> at <see cref="MaxBatchSize"/> so EF Core's
/// <c>WHERE ExternalStudentId IN (...)</c> read in <c>StudentWriter.UpsertBatchAsync</c>
/// cannot exceed SQL Server's <b>~2100 parameter limit</b> per query. Combined with the
/// other parameters the query may emit, 1000 keeps a comfortable safety margin.
/// </summary>
public sealed class StudentSyncOptionsValidator : IValidateOptions<StudentSyncOptions>
{
    /// <summary>
    /// Sourced from <see cref="SyncLimits.MaxBatchSize"/> so every module agrees on
    /// the same ceiling. Raising this requires switching the writer's IN-clause read
    /// to a chunked or temp-table strategy.
    /// </summary>
    public const int MaxBatchSize = SyncLimits.MaxBatchSize;

    public ValidateOptionsResult Validate(string? name, StudentSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail(
                $"{StudentSyncOptions.SectionName}:ConnectionString is required.");
        }

        if (options.BatchSize <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{StudentSyncOptions.SectionName}:BatchSize must be > 0 (was {options.BatchSize}).");
        }

        if (options.BatchSize > MaxBatchSize)
        {
            return ValidateOptionsResult.Fail(
                $"{StudentSyncOptions.SectionName}:BatchSize ({options.BatchSize}) must be <= {MaxBatchSize} " +
                $"to stay within SQL Server's ~2100 parameter limit per command.");
        }

        if (options.PushBatchSize <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{StudentSyncOptions.SectionName}:PushBatchSize must be > 0 (was {options.PushBatchSize}).");
        }

        if (options.PushBatchSize > MaxBatchSize)
        {
            return ValidateOptionsResult.Fail(
                $"{StudentSyncOptions.SectionName}:PushBatchSize ({options.PushBatchSize}) must be <= {MaxBatchSize}.");
        }

        // Defense in depth: a negative safety buffer would advance the cursor INTO the
        // future and cause data loss. A 0 buffer is permitted (operator opts out of
        // clawback). An unreasonably large buffer is allowed but warned about by
        // operators — we don't cap it here because some upstreams genuinely back-date
        // by hours.
        if (options.ExtractorSafetyBufferSeconds < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{StudentSyncOptions.SectionName}:ExtractorSafetyBufferSeconds must be >= 0 " +
                $"(was {options.ExtractorSafetyBufferSeconds}). Negative values advance the cursor into the future and cause data loss.");
        }

        return ValidateOptionsResult.Success;
    }
}
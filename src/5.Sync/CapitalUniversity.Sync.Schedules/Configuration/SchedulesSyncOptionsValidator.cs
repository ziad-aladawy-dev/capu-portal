using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Schedules.Configuration;

/// <summary>
/// Same SQL-Server parameter-limit-safe ceiling as the Students/Staff modules.
/// Distinct validator type per module so each module owns its own option diagnostics.
/// </summary>
public sealed class SchedulesSyncOptionsValidator : IValidateOptions<SchedulesSyncOptions>
{
    public const int MaxBatchSize = SyncLimits.MaxBatchSize;

    public ValidateOptionsResult Validate(string? name, SchedulesSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail($"{SchedulesSyncOptions.SectionName}:ConnectionString is required.");
        }

        if (options.BatchSize <= 0 || options.BatchSize > MaxBatchSize)
        {
            return ValidateOptionsResult.Fail(
                $"{SchedulesSyncOptions.SectionName}:BatchSize must be in (0, {MaxBatchSize}] (was {options.BatchSize}).");
        }

        if (options.PushBatchSize <= 0 || options.PushBatchSize > MaxBatchSize)
        {
            return ValidateOptionsResult.Fail(
                $"{SchedulesSyncOptions.SectionName}:PushBatchSize must be in (0, {MaxBatchSize}] (was {options.PushBatchSize}).");
        }

        if (options.ExtractorSafetyBufferSeconds < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{SchedulesSyncOptions.SectionName}:ExtractorSafetyBufferSeconds must be >= 0 " +
                $"(was {options.ExtractorSafetyBufferSeconds}).");
        }

        return ValidateOptionsResult.Success;
    }
}

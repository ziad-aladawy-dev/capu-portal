using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Finance.Configuration;

/// <summary>
/// Same SQL-Server parameter-limit-safe ceiling as the Students/Staff modules.
/// Distinct validator type per module so each module owns its own option diagnostics.
/// </summary>
public sealed class FinanceSyncOptionsValidator : IValidateOptions<FinanceSyncOptions>
{
    public const int MaxBatchSize = SyncLimits.MaxBatchSize;

    public ValidateOptionsResult Validate(string? name, FinanceSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail($"{FinanceSyncOptions.SectionName}:ConnectionString is required.");
        }

        if (options.BatchSize <= 0 || options.BatchSize > MaxBatchSize)
        {
            return ValidateOptionsResult.Fail(
                $"{FinanceSyncOptions.SectionName}:BatchSize must be in (0, {MaxBatchSize}] (was {options.BatchSize}).");
        }

        if (options.PushBatchSize <= 0 || options.PushBatchSize > MaxBatchSize)
        {
            return ValidateOptionsResult.Fail(
                $"{FinanceSyncOptions.SectionName}:PushBatchSize must be in (0, {MaxBatchSize}] (was {options.PushBatchSize}).");
        }

        if (options.ExtractorSafetyBufferSeconds < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{FinanceSyncOptions.SectionName}:ExtractorSafetyBufferSeconds must be >= 0 " +
                $"(was {options.ExtractorSafetyBufferSeconds}).");
        }

        return ValidateOptionsResult.Success;
    }
}

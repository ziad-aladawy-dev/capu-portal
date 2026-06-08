using CapitalUniversity.Sync.Abstractions.Models;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Registration.Configuration;

/// <summary>
/// SQL-Server parameter-limit-safe batch ceiling, matching the other sync
/// modules. Distinct validator type so this module owns its option diagnostics.
/// </summary>
public sealed class RegistrationSyncOptionsValidator : IValidateOptions<RegistrationSyncOptions>
{
    public const int MaxBatchSize = SyncLimits.MaxBatchSize;

    public ValidateOptionsResult Validate(string? name, RegistrationSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.BatchSize <= 0 || options.BatchSize > MaxBatchSize)
        {
            return ValidateOptionsResult.Fail(
                $"{RegistrationSyncOptions.SectionName}:BatchSize must be in (0, {MaxBatchSize}] (was {options.BatchSize}).");
        }

        if (options.ExtractorSafetyBufferSeconds < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{RegistrationSyncOptions.SectionName}:ExtractorSafetyBufferSeconds must be >= 0 " +
                $"(was {options.ExtractorSafetyBufferSeconds}).");
        }

        return ValidateOptionsResult.Success;
    }
}

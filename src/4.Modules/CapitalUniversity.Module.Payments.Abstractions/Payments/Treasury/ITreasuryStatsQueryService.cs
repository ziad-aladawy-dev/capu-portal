using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>Read-side aggregates for the admin finance dashboard.</summary>
public interface ITreasuryStatsQueryService
{
    Task<TreasuryStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default);
}

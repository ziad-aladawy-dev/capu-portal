using CapitalUniversity.Modules.Payments.Abstractions.Treasury;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Maps a free-text Treasury status string to a normalized
/// <see cref="SettlementOutcome"/>. ASSUMPTION on the exact tokens — adjust to
/// the confirmed Treasury contract. Unknown strings are treated as Pending
/// (safe: leaves the order untouched for the next reconciliation tick).
/// </summary>
public static class TreasuryStatusMapper
{
    public static SettlementOutcome Map(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return SettlementOutcome.Pending;

        return status.Trim().ToLowerInvariant() switch
        {
            "paid" or "success" or "succeeded" or "completed" or "captured" => SettlementOutcome.Paid,
            "failed" or "declined" or "cancelled" or "canceled" or "rejected" => SettlementOutcome.Failed,
            _ => SettlementOutcome.Pending,
        };
    }
}

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

/// <summary>Institution-wide treasury aggregates for the finance dashboard.</summary>
public sealed class TreasuryStatsResponse
{
    /// <summary>Σ TotalAmount of Paid fees.</summary>
    public decimal TotalCollected { get; set; }

    /// <summary>Σ TotalAmount of Pending + IncludedInOrder fees.</summary>
    public decimal OutstandingAmount { get; set; }

    public decimal RefundedAmount { get; set; }

    public int TotalFees { get; set; }

    public int TotalOrders { get; set; }

    public string Currency { get; set; } = "EGP";

    /// <summary>Fee counts/amounts keyed by <c>FeeStatus</c> (int).</summary>
    public List<TreasuryStatusSlice> FeesByStatus { get; set; } = new();

    /// <summary>Order counts/amounts keyed by <c>OrderStatus</c> (int).</summary>
    public List<TreasuryStatusSlice> OrdersByStatus { get; set; } = new();

    /// <summary>Settled revenue per calendar month (last 12 months, from Payments.PaidAt).</summary>
    public List<TreasuryMonthlyPoint> MonthlyCollected { get; set; } = new();
}

public sealed class TreasuryStatusSlice
{
    public int Status { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public sealed class TreasuryMonthlyPoint
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>Normalized outcome derived from a gateway status/webhook.</summary>
public enum SettlementOutcome
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Expired = 3,
}

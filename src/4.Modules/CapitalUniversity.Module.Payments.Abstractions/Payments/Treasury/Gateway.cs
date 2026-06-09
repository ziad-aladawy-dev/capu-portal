namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Supported HU Treasury payment gateways. Stored as int — values are stable;
/// never reorder. <c>bm</c> = Bank Misr (matches the Treasury route segment).
/// </summary>
public enum Gateway
{
    Mastercard = 0,
    BankMisr = 1,
    EFinance = 2,
}

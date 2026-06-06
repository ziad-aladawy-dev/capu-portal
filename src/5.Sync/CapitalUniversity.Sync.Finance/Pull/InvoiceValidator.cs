using CapitalUniversity.Sync.Abstractions.Contracts;

namespace CapitalUniversity.Sync.Finance.Pull;

public sealed class InvoiceValidator : IRecordValidator<InvoiceSyncDispatch>
{
    private const int CurrencyCodeLength = 3;

    public bool IsValid(InvoiceSyncDispatch record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Entity.ExternallySourced.ExternalId))
        {
            error = "ExternalId is required (sync merge key).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.ExternalStudentId))
        {
            error = "ExternalStudentId is required (every invoice attaches to a student).";
            return false;
        }

        if (record.Entity.TotalAmount <= 0m)
        {
            error = "TotalAmount must be > 0.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.Entity.Currency) || record.Entity.Currency.Length != CurrencyCodeLength)
        {
            error = $"Currency must be a {CurrencyCodeLength}-letter ISO 4217 code.";
            return false;
        }

        error = null;
        return true;
    }
}

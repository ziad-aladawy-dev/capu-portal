using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Modules.Payments.Abstractions.Manifest;

/// <summary>
/// Declares the Payments module's permission surface. Two resources:
///   <list type="bullet">
///     <item><c>invoices</c> — read + admin actions on invoices themselves.</item>
///     <item><c>transactions</c> — record + view payment provider activity
///       (typically held by ops + webhook handlers, not student-facing).</item>
///   </list>
/// Granting one does not imply the other — webhook handlers should hold
/// <c>transactions</c> only.
/// </summary>
public sealed class PaymentsPermissionManifest : IPermissionManifest
{
    private const string ResourceInvoices = "invoices";
    private const string DisplayInvoices = "Invoices";
    private const string ResourceTransactions = "transactions";
    private const string DisplayPaymentTransactions = "Payment Transactions";

    public string Module => "payments";
    public string DisplayName => "Payments";
    public string? Icon => "CreditCard";
    public int? OrderNumber => 8;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create(ResourceInvoices,     "View",      DisplayInvoices, 0),
        PermissionDefinition.Create(ResourceInvoices,     "Insert",    DisplayInvoices, 0),
        PermissionDefinition.Create(ResourceInvoices,     "EditClose", DisplayInvoices, 0),
        PermissionDefinition.Create(ResourceInvoices,     "Open",      DisplayInvoices, 0),
        PermissionDefinition.Create(ResourceInvoices,     "Delete",    DisplayInvoices, 0),

        PermissionDefinition.Create(ResourceTransactions, "View",      DisplayPaymentTransactions, 1),
        PermissionDefinition.Create(ResourceTransactions, "Insert",    DisplayPaymentTransactions, 1),
        PermissionDefinition.Create(ResourceTransactions, "EditClose", DisplayPaymentTransactions, 1),
        PermissionDefinition.Create(ResourceTransactions, "Open",      DisplayPaymentTransactions, 1),
        PermissionDefinition.Create(ResourceTransactions, "Delete",    DisplayPaymentTransactions, 1),
    };
}

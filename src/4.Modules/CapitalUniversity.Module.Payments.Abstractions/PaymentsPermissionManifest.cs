using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

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
    public string Module => "payments";
    public string DisplayName => "Payments";
    public string? Icon => "CreditCard";
    public int? OrderNumber => 8;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create("invoices",     "View",      "Invoices", 0),
        PermissionDefinition.Create("invoices",     "Insert",    "Invoices", 0),
        PermissionDefinition.Create("invoices",     "EditClose", "Invoices", 0),
        PermissionDefinition.Create("invoices",     "Open",      "Invoices", 0),
        PermissionDefinition.Create("invoices",     "Delete",    "Invoices", 0),

        PermissionDefinition.Create("transactions", "View",      "Payment Transactions", 1),
        PermissionDefinition.Create("transactions", "Insert",    "Payment Transactions", 1),
        PermissionDefinition.Create("transactions", "EditClose", "Payment Transactions", 1),
        PermissionDefinition.Create("transactions", "Open",      "Payment Transactions", 1),
        PermissionDefinition.Create("transactions", "Delete",    "Payment Transactions", 1),
    };
}

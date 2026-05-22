using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

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
    public string Module => "payments";
    public string DisplayName => LocalizedJson.Of("المدفوعات", "Payments");
    public string? Icon => "CreditCard";
    public int? OrderNumber => 8;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("invoices",     LocalizedJson.Of("الفواتير",            "Invoices"),             0),
        ResourceDefinition.WithCrudActions("transactions", LocalizedJson.Of("معاملات الدفع",       "Payment Transactions"), 1),
    };
}

using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions.Manifest;
using Microsoft.Extensions.DependencyInjection;

// Namespace deliberately uses the plural "Modules" segment to avoid clashing
// with the `Module` authorization entity (CapitalUniversity.Core.Domain.Authorization.Module).
namespace CapitalUniversity.Modules.Payments;

/// <summary>
/// DI entry point for the Payments module. Registers the Treasury payment stack
/// (receipts, mappings, fees, orders, payments, settlement, reconciliation) and
/// contributes the module assembly to
/// <see cref="CoreDbContext.ModuleConfigurationAssemblies"/> so EF picks up the
/// Treasury EF configurations at model-creating time.
/// </summary>
public static class PaymentsModuleExtensions
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services)
    {
        // Treasury payment repositories.
        services.AddScoped<Repositories.Treasury.ITreasuryReceiptRepository, Core.Infrastructure.Repositories.Treasury.TreasuryReceiptRepository>();
        services.AddScoped<Repositories.Treasury.IServiceReceiptMappingRepository, Core.Infrastructure.Repositories.Treasury.ServiceReceiptMappingRepository>();
        services.AddScoped<Repositories.Treasury.IStudentFeeRepository, Core.Infrastructure.Repositories.Treasury.StudentFeeRepository>();
        services.AddScoped<Repositories.Treasury.IOrderRepository, Core.Infrastructure.Repositories.Treasury.OrderRepository>();
        services.AddScoped<Repositories.Treasury.IPaymentRepository, Core.Infrastructure.Repositories.Treasury.PaymentRepository>();
        services.AddScoped<Repositories.Treasury.IPaymentTransactionRepository, Core.Infrastructure.Repositories.Treasury.PaymentTransactionRepository>();

        // Treasury services.
        services.AddScoped<Abstractions.Treasury.IFeeGenerationService, Application.Treasury.FeeGenerationService>();
        services.AddScoped<Abstractions.Treasury.IServiceReceiptMappingService, Application.Treasury.ServiceReceiptMappingService>();
        services.AddScoped<Abstractions.Treasury.IOrderService, Application.Treasury.OrderService>();
        services.AddScoped<Abstractions.Treasury.IStudentFeeQueryService, Application.Treasury.StudentFeeQueryService>();
        services.AddScoped<Abstractions.Treasury.IReceiptQueryService, Application.Treasury.ReceiptQueryService>();
        services.AddScoped<Abstractions.Treasury.IPaymentInitiationService, Application.Treasury.PaymentInitiationService>();
        services.AddScoped<Abstractions.Treasury.ISettlementService, Application.Treasury.SettlementService>();
        services.AddScoped<Abstractions.Treasury.IReconciliationService, Application.Treasury.ReconciliationService>();

        // B7 — Drains the "payments.fee.paid" outbox stream that SettlementService
        // publishes on every settlement. Without a handler for this MessageType the
        // dispatcher poisons one message per paid fee. See FeePaidEventHandler.
        services.AddScoped<Core.Abstractions.CrossCutting.Outbox.IOutboxMessageHandler,
            Application.Treasury.FeePaidEventHandler>();

        // Permission manifest — registered before the PermissionManifestRegistry
        // singleton is first resolved.
        services.AddSingleton<IPermissionManifest, PaymentsPermissionManifest>();

        var moduleAssembly = typeof(PaymentsModuleExtensions).Assembly;
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
        }

        return services;
    }
}

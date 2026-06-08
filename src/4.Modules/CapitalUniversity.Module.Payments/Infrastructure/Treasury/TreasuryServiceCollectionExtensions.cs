using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Application.Treasury;
using CapitalUniversity.Modules.Payments.Infrastructure.Treasury;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace CapitalUniversity.Modules.Payments;

/// <summary>
/// Wires the Treasury outbound integration: binds <see cref="TreasuryOptions"/>
/// and registers a typed <c>HttpClient</c> for <see cref="ITreasuryClient"/>
/// with the standard resilience handler. Callable from any host (the API host
/// in Phase 2; the Sync host in Phase 3 for the background pull job).
/// </summary>
public static class TreasuryServiceCollectionExtensions
{
    public static IServiceCollection AddTreasuryIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TreasuryOptions>(configuration.GetSection(TreasuryOptions.SectionName));

        var opts = configuration.GetSection(TreasuryOptions.SectionName).Get<TreasuryOptions>()
                   ?? new TreasuryOptions();

        services.AddHttpClient<ITreasuryClient, TreasuryClient>(client =>
        {
            // BaseUrl empty in dev/test → no calls are made unless an endpoint
            // is exercised; the registration stays valid either way.
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
            {
                client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            }
            client.Timeout = opts.Timeout;
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", opts.ApiKey);
            }
        }).AddStandardResilienceHandler();

        return services;
    }

    /// <summary>
    /// Registers the receipt synchronization service. Depends on
    /// <c>ITreasuryClient</c> (AddTreasuryIntegration) and <c>ICoreWriteGateway</c>
    /// (registered by the host). Call from the host that runs the pull job.
    /// </summary>
    public static IServiceCollection AddTreasuryReceiptSync(this IServiceCollection services)
    {
        services.AddScoped<IReceiptSyncService, ReceiptSyncService>();
        return services;
    }

    /// <summary>
    /// Registers settlement + reconciliation and the repositories they need, for
    /// a host that runs the reconciliation job but does not call AddPaymentsModule
    /// (e.g. the Sync host). IOutbox is optional and stays unregistered there;
    /// the webhook path in the API host drives event delivery.
    /// </summary>
    public static IServiceCollection AddTreasuryReconciliation(this IServiceCollection services)
    {
        services.AddScoped<Repositories.Treasury.IOrderRepository, Core.Infrastructure.Repositories.Treasury.OrderRepository>();
        services.AddScoped<Repositories.Treasury.IStudentFeeRepository, Core.Infrastructure.Repositories.Treasury.StudentFeeRepository>();
        services.AddScoped<Repositories.Treasury.IPaymentRepository, Core.Infrastructure.Repositories.Treasury.PaymentRepository>();
        services.AddScoped<Repositories.Treasury.IPaymentTransactionRepository, Core.Infrastructure.Repositories.Treasury.PaymentTransactionRepository>();
        services.AddScoped<Abstractions.Treasury.ISettlementService, Application.Treasury.SettlementService>();
        services.AddScoped<Abstractions.Treasury.IReconciliationService, Application.Treasury.ReconciliationService>();
        return services;
    }
}

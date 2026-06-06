using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Finance.Configuration;
using CapitalUniversity.Sync.Finance.Persistence;
using CapitalUniversity.Sync.Finance.Pull;
using CapitalUniversity.Sync.Finance.Push;
using CapitalUniversity.Sync.Finance.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Finance.DependencyInjection;

public static class FinanceSyncServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<FinanceSyncOptions>, FinanceSyncOptionsValidator>();

        services
            .AddOptions<FinanceSyncOptions>()
            .Bind(configuration.GetSection(FinanceSyncOptions.SectionName))
            .ValidateOnStart();

        var options = configuration.GetSection(FinanceSyncOptions.SectionName).Get<FinanceSyncOptions>()
            ?? new FinanceSyncOptions();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                $"{FinanceSyncOptions.SectionName}:ConnectionString is required.");
        }

        services.AddDbContext<FinanceSyncDbContext>(opts =>
            opts.UseSqlServer(
                options.ConnectionString,
                sql => sql.MigrationsHistoryTable(
                    "__FinanceSyncMigrationsHistory",
                    FinanceSyncDbContext.SchemaName)));

        services.AddSingleton<IExternalInvoiceSource, InMemoryExternalInvoiceSource>();

        services.AddSingleton<InMemoryExternalInvoiceSink>();
        services.AddSingleton<IExternalInvoiceSink>(sp =>
            sp.GetRequiredService<InMemoryExternalInvoiceSink>());

        services.AddTransient<InvoiceExtractor>();
        services.AddTransient<InvoiceMapper>();
        services.AddTransient<InvoiceValidator>();
        services.AddTransient<InvoiceWriter>();

        services.AddTransient<InvoiceOutboxExtractor>();
        services.AddTransient<InvoiceOutboxMapper>();
        services.AddTransient<InvoiceOutboxValidator>();
        services.AddTransient<InvoiceOutboxWriter>();

        services.AddSingleton<ISyncModule, FinanceSyncModule>();

        return services;
    }
}

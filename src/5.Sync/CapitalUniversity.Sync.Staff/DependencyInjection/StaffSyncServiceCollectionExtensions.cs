using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Staff.Configuration;
using CapitalUniversity.Sync.Staff.Persistence;
using CapitalUniversity.Sync.Staff.Pull;
using CapitalUniversity.Sync.Staff.Push;
using CapitalUniversity.Sync.Staff.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Staff.DependencyInjection;

public static class StaffSyncServiceCollectionExtensions
{
    public static IServiceCollection AddStaffSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<StaffSyncOptions>, StaffSyncOptionsValidator>();

        services
            .AddOptions<StaffSyncOptions>()
            .Bind(configuration.GetSection(StaffSyncOptions.SectionName))
            .ValidateOnStart();

        var options = configuration.GetSection(StaffSyncOptions.SectionName).Get<StaffSyncOptions>()
            ?? new StaffSyncOptions();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                $"{StaffSyncOptions.SectionName}:ConnectionString is required.");
        }

        services.AddDbContext<StaffSyncDbContext>(opts =>
            opts.UseSqlServer(
                options.ConnectionString,
                sql => sql.MigrationsHistoryTable(
                    "__StaffSyncMigrationsHistory",
                    StaffSyncDbContext.SchemaName)));

        services.AddSingleton<IExternalStaffSource, InMemoryExternalStaffSource>();

        services.AddSingleton<InMemoryExternalStaffSink>();
        services.AddSingleton<IExternalStaffSink>(sp =>
            sp.GetRequiredService<InMemoryExternalStaffSink>());

        services.AddTransient<StaffExtractor>();
        services.AddTransient<StaffMapper>();
        services.AddTransient<StaffValidator>();
        services.AddTransient<StaffWriter>();

        services.AddTransient<StaffOutboxExtractor>();
        services.AddTransient<StaffOutboxMapper>();
        services.AddTransient<StaffOutboxValidator>();
        services.AddTransient<StaffOutboxWriter>();

        services.AddSingleton<ISyncModule, StaffSyncModule>();

        return services;
    }
}
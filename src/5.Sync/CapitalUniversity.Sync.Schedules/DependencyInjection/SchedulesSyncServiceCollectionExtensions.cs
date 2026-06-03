using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Schedules.Configuration;
using CapitalUniversity.Sync.Schedules.Persistence;
using CapitalUniversity.Sync.Schedules.Pull;
using CapitalUniversity.Sync.Schedules.Push;
using CapitalUniversity.Sync.Schedules.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Schedules.DependencyInjection;

public static class SchedulesSyncServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulesSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<SchedulesSyncOptions>, SchedulesSyncOptionsValidator>();

        services
            .AddOptions<SchedulesSyncOptions>()
            .Bind(configuration.GetSection(SchedulesSyncOptions.SectionName))
            .ValidateOnStart();

        var options = configuration.GetSection(SchedulesSyncOptions.SectionName).Get<SchedulesSyncOptions>()
            ?? new SchedulesSyncOptions();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                $"{SchedulesSyncOptions.SectionName}:ConnectionString is required.");
        }

        services.AddDbContext<SchedulesSyncDbContext>(opts =>
            opts.UseSqlServer(
                options.ConnectionString,
                sql => sql.MigrationsHistoryTable(
                    "__SchedulesSyncMigrationsHistory",
                    SchedulesSyncDbContext.SchemaName)));

        services.AddSingleton<IExternalScheduleSlotSource, InMemoryExternalScheduleSlotSource>();

        services.AddSingleton<InMemoryExternalScheduleSlotSink>();
        services.AddSingleton<IExternalScheduleSlotSink>(sp =>
            sp.GetRequiredService<InMemoryExternalScheduleSlotSink>());

        services.AddTransient<ScheduleSlotExtractor>();
        services.AddTransient<ScheduleSlotMapper>();
        services.AddTransient<ScheduleSlotValidator>();
        services.AddTransient<ScheduleSlotWriter>();

        services.AddTransient<ScheduleSlotOutboxExtractor>();
        services.AddTransient<ScheduleSlotOutboxMapper>();
        services.AddTransient<ScheduleSlotOutboxValidator>();
        services.AddTransient<ScheduleSlotOutboxWriter>();

        services.AddSingleton<ISyncModule, SchedulesSyncModule>();

        return services;
    }
}

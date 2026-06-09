using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Registration.Configuration;
using CapitalUniversity.Sync.Registration.Pull;
using CapitalUniversity.Sync.Registration.Sources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Registration.DependencyInjection;

public static class RegistrationSyncServiceCollectionExtensions
{
    public static IServiceCollection AddRegistrationSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<RegistrationSyncOptions>, RegistrationSyncOptionsValidator>();

        services
            .AddOptions<RegistrationSyncOptions>()
            .Bind(configuration.GetSection(RegistrationSyncOptions.SectionName))
            .ValidateOnStart();

        // No DbContext: this module owns no outbox. Pull writes land in Core via
        // ICoreWriteGateway and the checkpoint lives in the shared SyncDbContext.
        services.AddSingleton<IExternalRegistrationSource, InMemoryExternalRegistrationSource>();

        services.AddTransient<RegistrationExtractor>();
        services.AddTransient<RegistrationMapper>();
        services.AddTransient<RegistrationValidator>();
        services.AddTransient<RegistrationWriter>();

        services.AddSingleton<ISyncModule, RegistrationSyncModule>();

        return services;
    }
}

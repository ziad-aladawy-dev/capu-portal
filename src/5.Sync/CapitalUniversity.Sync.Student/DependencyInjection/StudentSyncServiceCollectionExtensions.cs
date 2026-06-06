using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Student.Configuration;
using CapitalUniversity.Sync.Student.Persistence;
using CapitalUniversity.Sync.Student.Pull;
using CapitalUniversity.Sync.Student.Push;
using CapitalUniversity.Sync.Student.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Student.DependencyInjection;

public static class StudentSyncServiceCollectionExtensions
{
    public static IServiceCollection AddStudentSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<StudentSyncOptions>, StudentSyncOptionsValidator>();

        services
            .AddOptions<StudentSyncOptions>()
            .Bind(configuration.GetSection(StudentSyncOptions.SectionName))
            .ValidateOnStart();

        // Read once for the DbContext registration. Final validation runs at host start.
        var options = configuration.GetSection(StudentSyncOptions.SectionName).Get<StudentSyncOptions>()
            ?? new StudentSyncOptions();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                $"{StudentSyncOptions.SectionName}:ConnectionString is required.");
        }

        services.AddDbContext<StudentSyncDbContext>(opts =>
            opts.UseSqlServer(
                options.ConnectionString,
                sql => sql.MigrationsHistoryTable(
                    "__StudentSyncMigrationsHistory",
                    StudentSyncDbContext.SchemaName)));

        // External source: in-memory by default; production replaces with HTTP/SQL adapter.
        services.AddSingleton<IExternalStudentSource, InMemoryExternalStudentSource>();

        // External sink (Push): in-memory by default. Singleton so verification can
        // inspect accepted payloads across runs.
        services.AddSingleton<InMemoryExternalStudentSink>();
        services.AddSingleton<IExternalStudentSink>(sp =>
            sp.GetRequiredService<InMemoryExternalStudentSink>());

        // Pull pipeline parts — transient, resolved per run inside the module's scope.
        services.AddTransient<StudentExtractor>();
        services.AddTransient<StudentMapper>();
        services.AddTransient<StudentValidator>();
        services.AddTransient<StudentWriter>();

        // Push pipeline parts — transient, scoped DbContext capture per run.
        services.AddTransient<StudentOutboxExtractor>();
        services.AddTransient<StudentOutboxMapper>();
        services.AddTransient<StudentOutboxValidator>();
        services.AddTransient<StudentOutboxWriter>();

        // Module — singleton; resolves its parts per call.
        services.AddSingleton<ISyncModule, StudentSyncModule>();

        return services;
    }
}
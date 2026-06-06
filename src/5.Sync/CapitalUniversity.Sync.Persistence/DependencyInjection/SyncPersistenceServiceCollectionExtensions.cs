using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Sync.Persistence.DependencyInjection;

public static class SyncPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddSyncPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Sync persistence connection string required.", nameof(connectionString));
        }

        services.AddDbContext<SyncDbContext>(opts =>
            opts.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable("__SyncMigrationsHistory", SyncDbContext.SchemaName)));

        services.AddScoped<ISyncRunRepository, SyncRunRepository>();
        services.AddScoped<IFailureRepository, FailureRepository>();
        services.AddScoped<ISyncCheckpointStore, SyncCheckpointStore>();

        return services;
    }
}
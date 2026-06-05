using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;

namespace CapitalUniversity.Core.Infrastructure.Logging;

/// <summary>
/// Registers the Mongo-backed audit pipeline (queue → BufferedAppLogger →
/// AuditLogFlushWorker) standalone, for hosts that wire <c>CoreDbContext</c>
/// WITHOUT <c>AddCoreServices</c> — notably the Sync host, which deliberately
/// skips the full Core service graph (Redis / Identity / MediatR) but still
/// needs its writes into Core tables to land in the audit trail.
///
/// <para>
/// <see cref="IAppLogger"/> is registered as a singleton here (it only wraps the
/// singleton queue and holds no per-request state), so background jobs and the
/// scoped <c>CoreDbContext</c> can both resolve it. The API process keeps its
/// own scoped registration via <c>AddCoreServices</c> — call this ONLY where the
/// pipeline isn't already wired.
/// </para>
/// </summary>
public static class MongoAuditLoggingServiceCollectionExtensions
{
    public static IServiceCollection AddMongoAuditLogging(this IServiceCollection services, IConfiguration configuration)
    {
        // MongoDB.Driver 3.x removed the global GuidRepresentation; the log
        // metadata serialises boxed Guids through ObjectSerializer, which throws
        // unless a Standard representation is registered. Mirror AddCoreServices.
        try
        {
            MongoDB.Bson.Serialization.BsonSerializer.RegisterSerializer(
                new MongoDB.Bson.Serialization.Serializers.ObjectSerializer(
                    MongoDB.Bson.Serialization.BsonSerializer.LookupDiscriminatorConvention(typeof(object)),
                    MongoDB.Bson.GuidRepresentation.Standard,
                    MongoDB.Bson.Serialization.Serializers.ObjectSerializer.DefaultAllowedTypes));
        }
        catch (MongoDB.Bson.BsonSerializationException) { /* already registered */ }
        MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(
            new MongoDB.Bson.Serialization.Serializers.GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));

        services.Configure<MongoSettings>(configuration.GetSection("MongoSettings"));

        services.TryAddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoSettings>>().Value;
            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new InvalidOperationException("MongoDB ConnectionString is not configured in MongoSettings.");
            return new MongoClient(settings.ConnectionString);
        });

        // Async audit pipeline — same components as the API, but singleton-scoped
        // so background jobs (no request scope) can resolve the logger.
        services.TryAddSingleton<IAuditLogQueue>(_ => new ChannelAuditLogQueue());
        services.TryAddSingleton<IAppLogger, BufferedAppLogger>();
        services.AddHostedService<AuditLogFlushWorker>();

        return services;
    }
}

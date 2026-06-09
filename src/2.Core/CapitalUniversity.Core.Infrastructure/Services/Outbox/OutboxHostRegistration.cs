using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Core.Infrastructure.Services.Outbox;

/// <summary>
/// Registers <see cref="IOutbox"/> and its minimal dependencies for a background
/// host (e.g. the Sync host) that deliberately does NOT call AddCoreServices
/// (which would pull Mongo/Redis/Identity it has no use for).
///
/// <para>
/// Safe in a Hangfire job context: both <c>ExecutionContext</c> and
/// <c>CurrentCultureService</c> null-guard the absent <c>HttpContext</c>
/// (RequestId falls back to a fresh Guid; Language to the ambient culture).
/// The caller must also have registered <c>IHttpContextAccessor</c> (web hosts
/// do) and a scoped <c>CoreDbContext</c> (the outbox stages rows on it).
/// </para>
/// </summary>
public static class OutboxHostRegistration
{
    public static IServiceCollection AddOutboxForBackgroundHost(this IServiceCollection services)
    {
        services.AddScoped<IExecutionContext, CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication.ExecutionContext>();
        services.AddScoped<ICurrentCultureService, CapitalUniversity.Core.Application.CrossCutting.Localization.CurrentCultureService>();
        services.AddScoped<IOutbox, OutboxService>();
        return services;
    }
}

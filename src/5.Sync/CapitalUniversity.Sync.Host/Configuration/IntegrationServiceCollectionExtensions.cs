using CapitalUniversity.Sync.Staff.Sources;
using CapitalUniversity.Sync.Student.Sources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Phase X.2 fix #3 — central wiring for the HTTP-adapter override pattern.
/// Called from <c>Program.cs</c> AFTER <c>AddStudentSync</c> and
/// <c>AddStaffSync</c> so DI's last-registration-wins semantics make the HTTP
/// implementations the active <c>IExternalXSource</c>/<c>IExternalXSink</c>.
///
/// <para>
/// No-op when <c>Sync:Integration:UseHttpAdapters = false</c> (the default) so
/// the in-memory implementations stay active for dev/test.
/// </para>
/// </summary>
public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddSyncHttpAdaptersIfEnabled(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SyncIntegrationOptions>(
            configuration.GetSection(SyncIntegrationOptions.SectionName));

        var opts = configuration.GetSection(SyncIntegrationOptions.SectionName)
            .Get<SyncIntegrationOptions>() ?? new SyncIntegrationOptions();

        if (!opts.UseHttpAdapters)
        {
            return services;
        }

        // Fail fast: UseHttpAdapters=true must have a BaseUrl for every module.
        // Silently falling through to the in-memory sink would route real pushes
        // into a sink nobody reads in production — observable as "Successful sync"
        // with zero side effects upstream. Loud at startup is the correct trade.
        if (string.IsNullOrWhiteSpace(opts.Student.BaseUrl))
        {
            throw new InvalidOperationException(
                $"{SyncIntegrationOptions.SectionName}:UseHttpAdapters=true requires " +
                $"{SyncIntegrationOptions.SectionName}:Student:BaseUrl. " +
                "Leaving it empty would silently keep the in-memory Student sink — " +
                "real pushes would never reach the external system.");
        }
        if (string.IsNullOrWhiteSpace(opts.Staff.BaseUrl))
        {
            throw new InvalidOperationException(
                $"{SyncIntegrationOptions.SectionName}:UseHttpAdapters=true requires " +
                $"{SyncIntegrationOptions.SectionName}:Staff:BaseUrl. " +
                "Leaving it empty would silently keep the in-memory Staff sink — " +
                "real pushes would never reach the external system.");
        }

        // Student
        services.AddHttpClient<HttpExternalStudentSink>(client => ConfigureHttpClient(
            client, opts.Student.BaseUrl, opts.Student.Timeout, opts.Student.ApiKey))
            .AddStandardResilienceHandler();
        services.AddHttpClient<HttpExternalStudentSource>(client => ConfigureHttpClient(
            client, opts.Student.BaseUrl, opts.Student.Timeout, opts.Student.ApiKey))
            .AddStandardResilienceHandler();

        // Override the in-memory registrations done by AddStudentSync. Last-wins:
        // the next IExternalStudentSink resolved is the HTTP one.
        services.AddSingleton<IExternalStudentSink>(sp => sp.GetRequiredService<HttpExternalStudentSink>());
        services.AddSingleton<IExternalStudentSource>(sp => sp.GetRequiredService<HttpExternalStudentSource>());

        // Staff
        services.AddHttpClient<HttpExternalStaffSink>(client => ConfigureHttpClient(
            client, opts.Staff.BaseUrl, opts.Staff.Timeout, opts.Staff.ApiKey))
            .AddStandardResilienceHandler();
        services.AddHttpClient<HttpExternalStaffSource>(client => ConfigureHttpClient(
            client, opts.Staff.BaseUrl, opts.Staff.Timeout, opts.Staff.ApiKey))
            .AddStandardResilienceHandler();

        services.AddSingleton<IExternalStaffSink>(sp => sp.GetRequiredService<HttpExternalStaffSink>());
        services.AddSingleton<IExternalStaffSource>(sp => sp.GetRequiredService<HttpExternalStaffSource>());

        return services;
    }

    private static void ConfigureHttpClient(HttpClient client, string baseUrl, TimeSpan timeout, string? apiKey)
    {
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = timeout;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }
    }
}
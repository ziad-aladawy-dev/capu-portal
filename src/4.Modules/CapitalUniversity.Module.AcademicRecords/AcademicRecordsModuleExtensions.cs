using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;
using CapitalUniversity.Modules.AcademicRecords.Abstractions.Manifest;
using CapitalUniversity.Modules.AcademicRecords.Application;
using CapitalUniversity.Modules.AcademicRecords.Application.Pdf;
using CapitalUniversity.Modules.AcademicRecords.Repositories;
using Microsoft.Extensions.DependencyInjection;

// Plural "Modules" namespace segment matches the existing Module.Registration /
// Module.Student convention. csproj name keeps the singular
// "Module.AcademicRecords".
namespace CapitalUniversity.Modules.AcademicRecords;

/// <summary>
/// DI entry point for the Academic Records (Grades / Transcript) module.
/// Registers the read-only services, repository, transcript PDF renderer, and
/// permission manifest, and contributes the module assembly to
/// <see cref="CoreDbContext.ModuleConfigurationAssemblies"/> so EF picks up the
/// <c>StudentAcademicResult</c> / <c>AcademicSummarySnapshot</c> configurations
/// at model-creating time.
///
/// <para>
/// This is a read-heavy module: no validators, no unit-of-work, no caching —
/// academic outcomes are owned by the Sync Platform and written through the
/// generic <c>ICoreWriteGateway</c>, so there is no write surface to register
/// here. Call <c>AddCoreServices()</c> before <c>AddAcademicRecordsModule()</c>.
/// </para>
/// </summary>
public static class AcademicRecordsModuleExtensions
{
    public static IServiceCollection AddAcademicRecordsModule(this IServiceCollection services)
    {
        services.AddScoped<IAcademicRecordsRepository, AcademicRecordsRepository>();

        services.AddScoped<IAcademicRecordsService, AcademicRecordsService>();
        services.AddScoped<ITranscriptService, TranscriptService>();

        // Stateless PDF renderer — license is configured once in its static ctor.
        services.AddSingleton<ITranscriptPdfRenderer, QuestPdfTranscriptRenderer>();

        services.AddSingleton<IPermissionManifest, AcademicRecordsPermissionManifest>();

        var moduleAssembly = typeof(AcademicRecordsModuleExtensions).Assembly;
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
        }

        return services;
    }
}

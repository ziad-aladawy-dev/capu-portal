using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Student.Abstractions.Manifest;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation.DTOs;
using CapitalUniversity.Modules.Student.Application;
using CapitalUniversity.Modules.Student.Application.Validators;
using CapitalUniversity.Modules.Student.Repositories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

// Namespace deliberately uses the plural "Modules" segment to avoid clashing
// with the `Module` authorization entity (CapitalUniversity.Core.Domain.Authorization.Module).
// Otherwise unqualified `Module` references in callers resolve to this namespace
// instead of the entity type. csproj name keeps the singular "Module.Student".
namespace CapitalUniversity.Modules.Student;

/// <summary>
/// DI entry point for the Student Information module. Registers the
/// module's service, repository, validators, permission manifest, and
/// contributes the module assembly to
/// <see cref="CoreDbContext.ModuleConfigurationAssemblies"/> so EF picks up
/// <c>StudentProfileRecordConfiguration</c> at model-creating time.
///
/// <para>
/// Composition: call <c>AddCoreServices()</c> first, then
/// <c>AddStudentModule()</c>. Order matters only for the manifest registry
/// snapshot which is built lazily.
/// </para>
/// </summary>
public static class StudentModuleExtensions
{
    public static IServiceCollection AddStudentModule(this IServiceCollection services)
    {
        services.AddScoped<IStudentProfileRecordRepository, StudentProfileRecordRepository>();

        services.AddScoped<IStudentProfileService, StudentProfileService>();

        services.AddScoped<IValidator<UpsertStudentProfileRecordRequest>, UpsertStudentProfileRecordValidator>();
        services.AddScoped<IValidator<VerifyStudentProfileRecordRequest>, VerifyStudentProfileRecordValidator>();

        // Permission manifest — must be registered before the
        // PermissionManifestRegistry singleton is first resolved.
        services.AddSingleton<IPermissionManifest, StudentInformationPermissionManifest>();

        // Hand the EF configurations off to CoreDbContext. Idempotent — repeated
        // calls (e.g. WebApplicationFactory rebuilding the host across tests)
        // do not duplicate scans because ApplyConfigurationsFromAssembly only
        // applies a given configuration type once per model build.
        var moduleAssembly = typeof(StudentModuleExtensions).Assembly;
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
        }

        return services;
    }
}

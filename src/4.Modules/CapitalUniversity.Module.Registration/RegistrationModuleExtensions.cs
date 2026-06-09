using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Registration.Abstractions;
using CapitalUniversity.Modules.Registration.Abstractions.Manifest;
using CapitalUniversity.Modules.Registration.Application;
using CapitalUniversity.Modules.Registration.Repositories;
using Microsoft.Extensions.DependencyInjection;

// Plural "Modules" namespace segment matches the existing Module.Student /
// Module.CourseOffering convention — avoids clashing with the `Module`
// authorization entity in Core.Domain.Authorization. csproj name keeps the
// singular "Module.Registration".
namespace CapitalUniversity.Modules.Registration;

/// <summary>
/// DI entry point for the Registration (Registered Courses) module. Registers
/// the read-only service, repository, and permission manifest, and contributes
/// the module assembly to <see cref="CoreDbContext.ModuleConfigurationAssemblies"/>
/// so EF picks up <c>StudentRegisteredCourseConfiguration</c> at
/// model-creating time.
///
/// <para>
/// This is a read-heavy module: no validators, no unit-of-work, no caching —
/// registration data is owned by the Sync Platform and written through the
/// generic <c>ICoreWriteGateway</c>, so there is no write surface to register
/// here. Call <c>AddCoreServices()</c> before <c>AddRegistrationModule()</c>.
/// </para>
/// </summary>
public static class RegistrationModuleExtensions
{
    public static IServiceCollection AddRegistrationModule(this IServiceCollection services)
    {
        services.AddScoped<IStudentRegistrationRepository, StudentRegistrationRepository>();

        services.AddScoped<IStudentRegistrationService, StudentRegistrationService>();

        services.AddSingleton<IPermissionManifest, RegistrationPermissionManifest>();

        var moduleAssembly = typeof(RegistrationModuleExtensions).Assembly;
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
        }

        return services;
    }
}

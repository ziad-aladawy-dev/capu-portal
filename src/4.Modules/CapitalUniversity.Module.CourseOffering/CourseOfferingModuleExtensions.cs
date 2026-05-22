using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using CapitalUniversity.Modules.CourseOffering.Abstractions.Manifest;
using CapitalUniversity.Modules.CourseOffering.Application;
using CapitalUniversity.Modules.CourseOffering.Application.Validators;
using CapitalUniversity.Modules.CourseOffering.Repositories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

// Plural "Modules" namespace segment matches the existing Module.Student /
// Module.Payments convention — avoids clashing with the `Module`
// authorization entity in Core.Domain.Authorization. csproj name keeps the
// singular "Module.CourseOffering".
namespace CapitalUniversity.Modules.CourseOffering;

/// <summary>
/// DI entry point for the CourseOffering module. Registers the service,
/// repository, validators, permission manifest, and contributes the module
/// assembly to <see cref="CoreDbContext.ModuleConfigurationAssemblies"/> so
/// EF picks up <c>CourseOfferingConfiguration</c> at model-creating time.
///
/// <para>
/// Composition: call <c>AddCoreServices()</c> first, then
/// <c>AddCourseOfferingModule()</c>. Order matters only for the manifest
/// registry snapshot which is built lazily.
/// </para>
/// </summary>
public static class CourseOfferingModuleExtensions
{
    public static IServiceCollection AddCourseOfferingModule(this IServiceCollection services)
    {
        services.AddScoped<ICourseOfferingRepository, CourseOfferingRepository>();

        services.AddScoped<ICourseOfferingService, CourseOfferingService>();

        services.AddScoped<IValidator<CreateCourseOfferingRequest>, CreateCourseOfferingValidator>();
        services.AddScoped<IValidator<UpdateCourseOfferingRequest>, UpdateCourseOfferingValidator>();

        services.AddSingleton<IPermissionManifest, CourseOfferingPermissionManifest>();

        var moduleAssembly = typeof(CourseOfferingModuleExtensions).Assembly;
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
        }

        return services;
    }
}

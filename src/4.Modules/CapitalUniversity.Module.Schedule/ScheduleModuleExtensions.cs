using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;
using CapitalUniversity.Modules.Schedule.Abstractions.Manifest;
using CapitalUniversity.Modules.Schedule.Application;
using CapitalUniversity.Modules.Schedule.Application.Outbox;
using CapitalUniversity.Modules.Schedule.Application.Validators;
using CapitalUniversity.Modules.Schedule.Repositories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Modules.Schedule;

/// <summary>
/// DI entry point for the Schedule module. Registers service, repository,
/// validators, permission manifest, and contributes the module assembly to
/// <see cref="CoreDbContext.ModuleConfigurationAssemblies"/> so EF picks up
/// <c>ScheduleSlotConfiguration</c> at model-creating time.
///
/// <para>
/// Composition: call <c>AddCoreServices()</c> + <c>AddCourseOfferingModule()</c>
/// first — Schedule depends on <c>ICourseOfferingService</c> for parent
/// existence / scope checks.
/// </para>
/// </summary>
public static class ScheduleModuleExtensions
{
    public static IServiceCollection AddScheduleModule(this IServiceCollection services)
    {
        services.AddScoped<IScheduleSlotRepository, ScheduleSlotRepository>();

        services.AddScoped<IScheduleSlotService, ScheduleSlotService>();

        services.AddScoped<IValidator<CreateScheduleSlotRequest>, CreateScheduleSlotValidator>();
        services.AddScoped<IValidator<UpdateScheduleSlotRequest>, UpdateScheduleSlotValidator>();
        // Bundle of the two validators so ScheduleSlotService stays under the
        // 7-parameter convention; mirrors AcademicPlanValidators in core.
        services.AddScoped<ScheduleSlotValidators>();

        services.AddSingleton<IPermissionManifest, SchedulePermissionManifest>();

        // Outbox handlers for lifecycle events. Default behavior is to log
        // the fact via IAppLogger — replace any of these in DI to push the
        // event somewhere else (e.g. a downstream timetable replica).
        // Dispatcher routes one handler per MessageType, so a replacement is
        // a clean swap, not a merge.
        services.AddScoped<IOutboxMessageHandler, ScheduleSlotCreatedHandler>();
        services.AddScoped<IOutboxMessageHandler, ScheduleSlotUpdatedHandler>();
        services.AddScoped<IOutboxMessageHandler, ScheduleSlotDeletedHandler>();

        var moduleAssembly = typeof(ScheduleModuleExtensions).Assembly;
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
        }

        return services;
    }
}

using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using CapitalUniversity.Modules.StudentServices.Abstractions.Manifest;
using CapitalUniversity.Modules.StudentServices.Application;
using CapitalUniversity.Modules.StudentServices.Application.Outbox;
using CapitalUniversity.Modules.StudentServices.Application.Validators;
using CapitalUniversity.Modules.StudentServices.Repositories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

// Plural "Modules" segment matches the existing module convention so the
// `Module` authorization entity does not clash with the namespace.
namespace CapitalUniversity.Modules.StudentServices;

/// <summary>
/// DI entry point for the Student Services module. Registers services,
/// repositories, validators, permission manifest, and contributes the module
/// assembly to <see cref="CoreDbContext.ModuleConfigurationAssemblies"/> so
/// EF picks up the entity configurations at model-creating time.
///
/// <para>
/// Composition: call <c>AddCoreServices()</c> + <c>AddPaymentsModule()</c>
/// first — Student Services depends on <c>IFeeCreationService</c> from the
/// Payments module to author fee invoices on submit.
/// </para>
/// </summary>
public static class StudentServicesModuleExtensions
{
    public static IServiceCollection AddStudentServicesModule(this IServiceCollection services)
    {
        services.AddScoped<IStudentServiceRepository, StudentServiceRepository>();
        services.AddScoped<IStudentServiceRequestRepository, StudentServiceRequestRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();

        services.AddScoped<IStudentServiceService, StudentServiceService>();
        services.AddScoped<IStudentServiceRequestService, StudentServiceRequestService>();
        services.AddScoped<IWorkflowService, WorkflowService>();

        services.AddScoped<IValidator<CreateStudentServiceRequest>, CreateStudentServiceValidator>();
        services.AddScoped<IValidator<UpdateStudentServiceRequest>, UpdateStudentServiceValidator>();
        services.AddScoped<IValidator<SubmitStudentServiceRequestRequest>, SubmitStudentServiceRequestValidator>();
        services.AddScoped<IValidator<CancelStudentServiceRequestRequest>, CancelStudentServiceRequestValidator>();
        services.AddScoped<IValidator<ApproveStudentServiceRequestRequest>, ApproveStudentServiceRequestValidator>();
        services.AddScoped<IValidator<RejectStudentServiceRequestRequest>, RejectStudentServiceRequestValidator>();
        services.AddScoped<IValidator<MoveRequestWorkflowStateRequest>, MoveRequestWorkflowStateValidator>();
        services.AddScoped<IValidator<CreateWorkflowDefinitionRequest>, CreateWorkflowDefinitionValidator>();

        // Validator bundles — keep the service constructors within the
        // project's parameter-count convention (mirrors ScheduleSlotValidators).
        services.AddScoped<StudentServiceValidators>();
        services.AddScoped<StudentServiceRequestValidators>();

        services.AddSingleton<IPermissionManifest, StudentServicesPermissionManifest>();

        // Outbox consumer — picks up <c>payments.invoice.paid</c> rows from
        // the Payments producer and auto-advances WaitingPayment requests.
        // The dispatcher routes one handler per MessageType, so this is the
        // sole consumer of the invoice-paid fact unless someone else
        // explicitly opts in.
        services.AddScoped<IOutboxMessageHandler, InvoicePaidEventHandler>();

        // Treasury fee path: advance WaitingPayment requests on payments.fee.paid.
        services.AddScoped<IOutboxMessageHandler, FeePaidEventHandler>();

        var moduleAssembly = typeof(StudentServicesModuleExtensions).Assembly;
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
        }

        return services;
    }
}

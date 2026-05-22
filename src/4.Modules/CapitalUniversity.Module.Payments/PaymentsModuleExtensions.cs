using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Modules.Payments.Application;
using CapitalUniversity.Modules.Payments.Application.Validators;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using CapitalUniversity.Modules.Payments.Repositories;
using CapitalUniversity.Modules.Payments.Abstractions.Manifest;

// Namespace deliberately uses the plural "Modules" segment to avoid clashing
// with the `Module` authorization entity (CapitalUniversity.Core.Domain.Authorization.Module).
// Otherwise unqualified `Module` references in callers resolve to this namespace
// instead of the entity type. csproj name keeps the singular "Module.Payments".
namespace CapitalUniversity.Modules.Payments;

/// <summary>
/// DI entry point for the Payments module. Registers the module's services,
/// repository, validators, permission manifest, and contributes the module
/// assembly to <see cref="CoreDbContext.ModuleConfigurationAssemblies"/> so
/// EF picks up <c>InvoiceConfiguration</c>, <c>InvoiceItemConfiguration</c>,
/// and <c>PaymentTransactionConfiguration</c> at model-creating time.
///
/// <para>
/// Composition: call <c>AddCoreServices()</c> first, then
/// <c>AddPaymentsModule()</c>. Order matters only for the manifest registry
/// snapshot which is built lazily.
/// </para>
/// </summary>
public static class PaymentsModuleExtensions
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services)
    {
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IFeeCreationService, FeeCreationService>();
        services.AddScoped<IPaymentVerificationService, PaymentVerificationService>();

        services.AddScoped<IValidator<CreateInvoiceRequest>, CreateInvoiceValidator>();
        services.AddScoped<IValidator<RecordPaymentRequest>, RecordPaymentValidator>();

        // Permission manifest — must be registered before the
        // PermissionManifestRegistry singleton is first resolved.
        services.AddSingleton<IPermissionManifest, PaymentsPermissionManifest>();

        // Hand the EF configurations off to CoreDbContext. Idempotent — repeated
        // calls (e.g. WebApplicationFactory rebuilding the host across tests)
        // do not duplicate scans because ApplyConfigurationsFromAssembly only
        // applies a given configuration type once per model build.
        var moduleAssembly = typeof(PaymentsModuleExtensions).Assembly;
        if (!CoreDbContext.ModuleConfigurationAssemblies.Contains(moduleAssembly))
        {
            CoreDbContext.ModuleConfigurationAssemblies.Add(moduleAssembly);
        }

        return services;
    }
}

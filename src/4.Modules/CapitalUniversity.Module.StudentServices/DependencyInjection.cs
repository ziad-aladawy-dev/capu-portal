using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Modules;
using CapitalUniversity.Module.StudentServices.Abstractions.Manifest;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Application;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Module.StudentServices;

public static class DependencyInjection
{
    public static IServiceCollection AddStudentServicesModule(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringKey = "DefaultConnection")
    {
        services.AddDbContext<StudentServicesDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString(connectionStringKey),
                sqlOptions => sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_StudentServices", "StudentServices")));

        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IStudentRequestRepository, StudentRequestRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();

        services.AddScoped<IServiceManagementService, ServiceManagementService>();
        services.AddScoped<IStudentRequestService, StudentRequestService>();
        services.AddScoped<IWorkflowManagementService, WorkflowManagementService>();
        services.AddScoped<IDashboardStatisticsService, DashboardStatisticsService>();
        services.AddScoped<IFileUploadService, FileUploadService>();

        services.AddSingleton<IPermissionManifest, StudentServicesPermissionManifest>();

        services.AddSingleton<IManifest, StudentServicesManifest>();

        return services;
    }

    public static IMvcBuilder AddStudentServicesControllers(this IMvcBuilder mvcBuilder)
    {
        var assembly = typeof(StudentServicesManifest).Assembly;
        mvcBuilder.AddApplicationPart(assembly);
        return mvcBuilder;
    }
}
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.StaffManagement;
using CapitalUniversity.Core.Abstractions.Students;
using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Application.CrossCutting.Audit;
using CapitalUniversity.Core.Application.Auth.Authentication;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Application.StaffManagement;
using CapitalUniversity.Core.Application.Students;
using CapitalUniversity.Core.Application.UniversityStructure;
using CapitalUniversity.Core.Abstractions.Semesters;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Application.Semesters;
using CapitalUniversity.Core.Application.Semesters.Validators;
using CapitalUniversity.Core.Infrastructure.Services.Semesters;
using CapitalUniversity.Core.Infrastructure.Repositories;
using CapitalUniversity.Core.Infrastructure.Services.Authentication;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using CapitalUniversity.Core.Infrastructure.Logging;
using CapitalUniversity.Core.Infrastructure.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Application.CrossCutting.Caching;
using CapitalUniversity.Core.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Options;
using CapitalUniversity.Core.Abstractions.Repositories;

namespace CapitalUniversity.Core.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        // Register Caching
        if (configuration.GetValue<bool>("Redis:Enabled"))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
            });
            services.AddScoped<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddScoped<ICacheService, MemoryCacheService>();
        }
        services.Configure<MongoSettings>(configuration.GetSection("MongoSettings"));

        // 2. Register IMongoClient using the configured options
        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;

            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new InvalidOperationException("MongoDB ConnectionString is not configured in MongoSettings.");

            return new MongoClient(settings.ConnectionString);
        });

        // 3. Register IMongoDatabase
        services.AddScoped(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;

            if (string.IsNullOrWhiteSpace(settings.DatabaseName))
                throw new InvalidOperationException("MongoDB DatabaseName is not configured in MongoSettings.");

            var client = sp.GetRequiredService<IMongoClient>();
            return client.GetDatabase(settings.DatabaseName);
        });
        // Register Validators
        services.AddValidatorsFromAssembly(typeof(IStudentService).Assembly);

        services.AddScoped<IStructureNodeRepository, StructureNodeRepository>();

        services.AddScoped<IUniversityStructureService, UniversityStructureService>();

        services.AddScoped<IStructureLookupService, StructureLookupService>();

        services.AddScoped<IStudentRepository, StudentRepository>();

        services.AddScoped<IStudentService, StudentService>();

        services.AddScoped<IStaffRepository, StaffRepository>();

        services.AddScoped<IStaffService, StaffService>();

        services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
        services.AddScoped<ISemesterRepository, SemesterRepository>();

        services.AddScoped<IAcademicYearService, AcademicYearService>();
        services.AddScoped<ISemesterService, SemesterService>();

        services.AddScoped<IValidator<CreateAcademicYearRequest>, CreateAcademicYearValidator>();
        services.AddScoped<IValidator<(Guid Id, UpdateAcademicYearRequest Request)>, UpdateAcademicYearValidator>();
        services.AddScoped<IValidator<CreateSemesterRequest>, CreateSemesterValidator>();
        services.AddScoped<IValidator<(Guid Id, UpdateSemesterRequest Request)>, UpdateSemesterValidator>();

        services.AddHostedService<AcademicTimelineBackgroundService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Auth Services
        services.AddScoped<IUserCredentialResolver, UserCredentialResolver>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        services.AddScoped<IRequestContext, RequestContext>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IExecutionContext, CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication.ExecutionContext>();

        // Permission Services
        services.AddScoped<IScopeResolver, ScopeResolver>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IPermissionManagementService, PermissionManagementService>();
        services.AddScoped<CapitalUniversity.Core.Infrastructure.Services.Authorization.Queries.PermissionTreeQueryHandler>();

        // Localization
        services.AddScoped<ICurrentCultureService, CurrentCultureService>();
        services.AddScoped<ILocalizationService, LocalizationService>();

        // Logging & Audit

        services.AddScoped<ILoggerService, SerilogLoggerService>();
        services.AddScoped<IAppLogger, MongoLoggerService>();

        // Notifications
        services.AddScoped<INotificationService, NotificationService>();

        // Note: IMongoClient should be registered in the API layer with proper connection string
        // but adding a placeholder if not present to allow Core tests/services to resolve.
        // services.AddSingleton<IMongoClient>(...);

        return services;
    }
}

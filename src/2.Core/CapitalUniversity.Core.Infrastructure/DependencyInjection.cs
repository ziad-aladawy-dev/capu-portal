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

        services.Configure<CapitalUniversity.Core.Abstractions.CrossCutting.Caching.PermissionCacheOptions>(
            configuration.GetSection(CapitalUniversity.Core.Abstractions.CrossCutting.Caching.PermissionCacheOptions.SectionName));

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
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IAcademicPlanRepository, AcademicPlanRepository>();
        // IInvoiceRepository moved to Module.Payments — registered by AddPaymentsModule().
        // IStudentProfileRecordRepository moved to Module.Student — registered by AddStudentModule().

        services.AddScoped<IAcademicYearService, AcademicYearService>();
        services.AddScoped<ISemesterService, SemesterService>();
        services.AddScoped<CapitalUniversity.Core.Abstractions.Courses.ICourseService,
                           CapitalUniversity.Core.Application.Courses.CourseService>();
        services.AddScoped<CapitalUniversity.Core.Application.Courses.AcademicPlanValidators>();
        services.AddScoped<CapitalUniversity.Core.Abstractions.Courses.IAcademicPlanService,
                           CapitalUniversity.Core.Application.Courses.AcademicPlanService>();
        // IInvoiceService / IFeeCreationService / IPaymentVerificationService
        // moved to Module.Payments — registered by AddPaymentsModule().
        // IStudentProfileService moved to Module.Student — registered by AddStudentModule().

        services.AddScoped<IValidator<CreateAcademicYearRequest>, CreateAcademicYearValidator>();
        services.AddScoped<IValidator<(Guid Id, UpdateAcademicYearRequest Request)>, UpdateAcademicYearValidator>();
        services.AddScoped<IValidator<CreateSemesterRequest>, CreateSemesterValidator>();
        services.AddScoped<IValidator<(Guid Id, UpdateSemesterRequest Request)>, UpdateSemesterValidator>();
        services.AddScoped<IValidator<CapitalUniversity.Core.Infrastructure.Services.Roles.Commands.CreateRoleRequest>,
                           CapitalUniversity.Core.Infrastructure.Services.Roles.Validators.CreateRoleValidator>();
        services.AddScoped<IValidator<CapitalUniversity.Core.Infrastructure.Services.Roles.Commands.UpdateRoleRequest>,
                           CapitalUniversity.Core.Infrastructure.Services.Roles.Validators.UpdateRoleValidator>();
        services.AddScoped<IValidator<CapitalUniversity.Core.Abstractions.Courses.DTOs.CreateCourseRequest>,
                           CapitalUniversity.Core.Application.Courses.Validators.CreateCourseValidator>();
        services.AddScoped<IValidator<(Guid Id, CapitalUniversity.Core.Abstractions.Courses.DTOs.UpdateCourseRequest Request)>,
                           CapitalUniversity.Core.Application.Courses.Validators.UpdateCourseValidator>();
        services.AddScoped<IValidator<CapitalUniversity.Core.Abstractions.Courses.DTOs.CreateAcademicPlanRequest>,
                           CapitalUniversity.Core.Application.Courses.Validators.CreateAcademicPlanValidator>();
        services.AddScoped<IValidator<(Guid Id, CapitalUniversity.Core.Abstractions.Courses.DTOs.UpdateAcademicPlanRequest Request)>,
                           CapitalUniversity.Core.Application.Courses.Validators.UpdateAcademicPlanValidator>();
        services.AddScoped<IValidator<CapitalUniversity.Core.Abstractions.Courses.DTOs.AddPlanCourseRequest>,
                           CapitalUniversity.Core.Application.Courses.Validators.AddPlanCourseValidator>();
        // CreateInvoiceRequest + RecordPaymentRequest validators moved to
        // Module.Payments — registered by AddPaymentsModule().
        // UpsertStudentProfileRecordRequest + VerifyStudentProfileRecordRequest
        // validators moved to Module.Student — registered by AddStudentModule().

        services.AddHostedService<AcademicTimelineBackgroundService>();

        services.AddScoped<UnitOfWorkRepositories>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Auth Services
        services.AddScoped<IUserCredentialResolver, UserCredentialResolver>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        // SessionVersion is checked on every authenticated request via the
        // middleware. The decorator caches the per-user lookup through
        // ICacheService and invalidates write-through on Increment.
        services.AddOptions<CapitalUniversity.Core.Abstractions.CrossCutting.Caching.SessionVersionCacheOptions>()
            .Bind(configuration.GetSection(CapitalUniversity.Core.Abstractions.CrossCutting.Caching.SessionVersionCacheOptions.SectionName));
        services.AddScoped<SessionVersionService>();
        services.AddScoped<ISessionVersionService>(sp => new CachedSessionVersionService(
            sp.GetRequiredService<SessionVersionService>(),
            sp.GetRequiredService<CapitalUniversity.Core.Abstractions.CrossCutting.Caching.ICacheService>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<
                CapitalUniversity.Core.Abstractions.CrossCutting.Caching.SessionVersionCacheOptions>>()));

        services.AddOptions<RefreshTokenSettings>()
            .Bind(configuration.GetSection(RefreshTokenSettings.SectionName));
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services.AddScoped<IRequestContext, RequestContext>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IExecutionContext, CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication.ExecutionContext>();

        // Permission Services
        services.AddScoped<IScopeResolver, ScopeResolver>();
        services.AddScoped<IPermissionService, PermissionService>();

        // P1.1 — row-level scoped authorization for Invoice / AcademicPlan /
        // StudentProfileRecord. Scoped so the per-request memoisation in
        // UserScope and EffectiveScope holds for the lifetime of one HTTP
        // request and disposes with the request DbContext.
        services.AddScoped<IUserScope, CapitalUniversity.Core.Infrastructure.Services.Authorization.UserScope>();
        services.AddScoped<IEffectiveScope, CapitalUniversity.Core.Infrastructure.Services.Authorization.EffectiveScope>();
        services.AddScoped<PermissionCacheCoordinator>(sp => new PermissionCacheCoordinator(
            sp.GetRequiredService<ICacheService>(),
            sp.GetRequiredService<IOptions<PermissionCacheOptions>>().Value,
            sp.GetService<IPermissionCacheInvalidator>()));
        services.AddScoped<IPermissionManagementService, PermissionManagementService>();
        services.AddScoped<IPermissionCacheInvalidator, PermissionCacheInvalidator>();

        // Role command/query handlers — concrete classes injected directly into
        // RolesController. No interface abstraction yet (kept simple per the
        // handler's "// Keeping it simple as a service/handler for now" note).
        services.AddScoped<CapitalUniversity.Core.Infrastructure.Services.Roles.Commands.CreateRoleCommandHandler>();
        services.AddScoped<CapitalUniversity.Core.Infrastructure.Services.Roles.Commands.UpdateRoleCommandHandler>();
        services.AddScoped<CapitalUniversity.Core.Infrastructure.Services.Roles.Commands.DeleteRoleCommandHandler>();
        services.AddScoped<CapitalUniversity.Core.Infrastructure.Services.Roles.Queries.GetRoleByIdQueryHandler>();
        services.AddScoped<CapitalUniversity.Core.Infrastructure.Services.Roles.Queries.GetRolesQueryHandler>();
        services.AddScoped<CapitalUniversity.Core.Infrastructure.Services.Roles.Queries.GetRoleMembersQueryHandler>();

        // Security-event audit logger (PR-B4) — routes denials, auth failures,
        // token revocations + role assignment changes through the async audit
        // pipeline. Wired as Scoped so it picks up the per-request HttpContext.
        services.AddScoped<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Audit.IAuthAuditLogger,
                           CapitalUniversity.Core.Infrastructure.Services.Authorization.AuthAuditLogger>();

        // Permission Manifest System (PR-B1) — each module declares its own permissions
        // via an IPermissionManifest. The registry validates duplicates at startup; the
        // synchronizer ensures Module + Service rows exist for every declared permission.
        // Each domain owns its own manifest. AuthorizationPermissionManifest
        // stays in CrossCutting.Auth — it IS the Auth meta-module declaring its
        // own permission surface. The others live next to their domain code.
        services.AddSingleton<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifest,
                              CapitalUniversity.Core.Application.Semesters.Authorization.AcademicsPermissionManifest>();
        services.AddSingleton<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifest,
                              CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest.AuthorizationPermissionManifest>();
        services.AddSingleton<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifest,
                              CapitalUniversity.Core.Application.CrossCutting.Notifications.Authorization.NotificationsPermissionManifest>();
        services.AddSingleton<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifest,
                              CapitalUniversity.Core.Application.Courses.Authorization.CoursesPermissionManifest>();
        // PaymentsPermissionManifest moved to Module.Payments.Abstractions —
        // registered by AddPaymentsModule().
        // StudentInformationPermissionManifest moved to
        // Module.Student.Abstractions — registered by AddStudentModule().
        services.AddSingleton<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifestRegistry,
                              CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest.PermissionManifestRegistry>();
        services.AddSingleton<CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest.ManifestActionExpander>();
        services.AddScoped<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifestSynchronizer,
                           CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest.PermissionManifestSynchronizer>();
        services.AddScoped<
            CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Permissions.Queries.IPermissionTreeQueryHandler,
            CapitalUniversity.Core.Infrastructure.Services.Authorization.Queries.PermissionTreeQueryHandler>();

        // Outbox — producer service, dispatcher hosted in API project. Handlers register
        // themselves via AddSingleton<IOutboxMessageHandler, ...> on the caller side; we
        // ship the in-app NotificationOutboxHandler here as the canonical example.
        services.Configure<CapitalUniversity.Core.Abstractions.CrossCutting.Outbox.OutboxOptions>(
            configuration.GetSection(CapitalUniversity.Core.Abstractions.CrossCutting.Outbox.OutboxOptions.SectionName));
        services.AddScoped<CapitalUniversity.Core.Abstractions.CrossCutting.Outbox.IOutbox,
                           CapitalUniversity.Core.Infrastructure.Services.Outbox.OutboxService>();
        services.AddScoped<CapitalUniversity.Core.Abstractions.CrossCutting.Outbox.IOutboxPoisonQueue,
                           CapitalUniversity.Core.Infrastructure.Services.Outbox.OutboxPoisonQueue>();
        services.AddScoped<CapitalUniversity.Core.Abstractions.CrossCutting.Outbox.IOutboxMessageHandler,
                           CapitalUniversity.Core.Infrastructure.Services.Outbox.NotificationOutboxHandler>();
        services.AddHostedService<CapitalUniversity.Core.Infrastructure.Services.Outbox.OutboxDispatcher>();

        // Localization
        services.AddScoped<ICurrentCultureService, CurrentCultureService>();
        services.AddScoped<ILocalizationService, LocalizationService>();

        // Logging & Audit

        services.AddScoped<ILoggerService, SerilogLoggerService>();

        // Async audit pipeline (PR-D1): BufferedAppLogger captures HttpContext fields
        // synchronously and stages a LogEntry on a bounded Channel; AuditLogFlushWorker
        // drains the channel and writes to Mongo out of band. MongoLoggerService is
        // still available for callers that need a synchronous write.
        services.AddSingleton<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAuditLogQueue>(
            _ => new CapitalUniversity.Core.Infrastructure.Logging.ChannelAuditLogQueue());
        services.AddScoped<IAppLogger, CapitalUniversity.Core.Infrastructure.Logging.BufferedAppLogger>();
        services.AddHostedService<CapitalUniversity.Core.Infrastructure.Logging.AuditLogFlushWorker>();

        // Notifications
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}

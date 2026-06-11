using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.API.Seeders;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Infrastructure;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Persistence.Seeders;
using CapitalUniversity.Modules.AcademicRecords;
using CapitalUniversity.Module.StudentServices;
using CapitalUniversity.Module.StudentServices.Abstractions.Hubs;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Seeders;
using CapitalUniversity.Modules.CourseOffering;
using CapitalUniversity.Modules.Payments;
using CapitalUniversity.Modules.Registration;
using CapitalUniversity.Modules.Schedule;
using CapitalUniversity.Modules.Student;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure JwtSettings & Options
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();

// B4 — The committed appsettings ship a development PLACEHOLDER signing key.
// MinLength(32) on JwtSettings.Key only checks length, so the placeholder
// passes ValidateOnStart and would silently sign production tokens with a
// publicly-known secret (full auth-bypass via forged HS256 tokens). Fail fast
// outside Development unless a real, unique key is supplied via env/secret
// store. The constant below MUST match the placeholder in appsettings.json.
const string JwtDevPlaceholderKey = "YourSuperSecretKeyAtLeast32CharactersLong!";
if (!builder.Environment.IsDevelopment()
    && (string.IsNullOrWhiteSpace(jwtSettings?.Key) || jwtSettings.Key == JwtDevPlaceholderKey))
{
    throw new InvalidOperationException(
        "Jwt:Key is unset or is the built-in development placeholder. Supply a unique " +
        "secret of at least 32 characters via environment variable or secret store " +
        "(e.g. Jwt__Key) before running outside the Development environment.");
}

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings?.Issuer,
            ValidAudience = jwtSettings?.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Key ?? string.Empty))
        };
    });

// ProblemDetails & Exception Handling
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

if (builder.Environment.EnvironmentName != "Testing")
{
    builder.Services.AddDbContext<CoreDbContext>(options =>
        options
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                // Transient SQL errors (deadlocks with retryable codes, brief
                // connection drops) re-execute the command without surfacing as
                // 500s. Six retries with EF's default exponential backoff is
                // the SqlServer provider default. No retryable exceptions are
                // added beyond the built-in list.
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 6,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));
}

builder.Services.AddCoreServices(builder.Configuration);

// Module registrations — must run after AddCoreServices so any shared
// infrastructure (cache, UoW, scope services) is already in the container
// before the module wires its services that depend on those interfaces.
builder.Services.AddPaymentsModule();
// Treasury outbound integration (Phase 2) — typed HttpClient + options.
builder.Services.AddTreasuryIntegration(builder.Configuration);
builder.Services.AddStudentModule();
builder.Services.AddCourseOfferingModule();
// Schedule depends on ICourseOfferingService for parent existence + scope
// checks — registered AFTER CourseOffering so the resolver finds the
// dependency at construction time.
builder.Services.AddScheduleModule();
// Registration is a read-only module over sync-sourced registration data; it
// depends only on Core (scope service + DbContext), so order is unconstrained.
builder.Services.AddRegistrationModule();
// Academic Records (Grades / Transcript) is read-only over sync-sourced academic
// outcomes; it reads registration data (StudentRegisteredCourse) and the active
// academic plan, so it is registered AFTER Registration. Depends only on Core +
// Registration types, no construction-time service dependency on either.
builder.Services.AddAcademicRecordsModule();
// Student Services depends on IFeeCreationService (Payments) for fee
// authoring on submit — registered AFTER Payments so the resolver finds
// the dependency at construction time.
//builder.Services.AddStudentServicesModule();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

builder.Services.AddOptions<SessionVersionOptions>()
    .Bind(builder.Configuration.GetSection(SessionVersionOptions.SectionName));

// Every endpoint requires an authenticated principal by default. Login, refresh
// (anon — caller carries an expired/expiring token), health, and swagger opt out
// with [AllowAnonymous] or anonymous mappings below.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddControllers();

builder.Services.AddStudentServicesModule(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddControllers().AddStudentServicesControllers();

// Runtime Hardening Plan §1.1 — Validation pipeline consolidation.
// Suppress MVC's automatic 400 short-circuit on invalid ModelState so every
// request reaches the application service. The service layer owns FluentValidation
// invocation and throws our domain ValidationException; GlobalExceptionHandler
// then produces a single, localized ProblemDetails shape for every validation
// failure. Without this, FluentValidation's auto-validation filter would emit
// MVC's default {errors:{...}} payload before our services run, producing
// inconsistent responses and bypassing localization.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
// AddFluentValidationAutoValidation is retained for validator discovery / DI
// registration only — the filter still runs but no longer triggers the
// short-circuit since SuppressModelStateInvalidFilter is true.
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// B6 — CORS origins. Outside Development, AllowedOrigins MUST be configured
// explicitly (deployment-specific). The previous localhost fallback meant a
// production deploy with no AllowedOrigins silently allowed only localhost —
// blocking the real SPA — or invited a permissive "*" workaround. Fail fast in
// non-Development if origins are missing; keep the localhost dev fallback.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
if (!builder.Environment.IsDevelopment() && (allowedOrigins is null || allowedOrigins.Length == 0))
{
    throw new InvalidOperationException(
        "AllowedOrigins is not configured. Set the AllowedOrigins array (the SPA's " +
        "public origin(s), e.g. https://portal.example.edu) via configuration or " +
        "environment (AllowedOrigins__0) before running outside the Development environment.");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins ?? ["http://localhost:5173", "http://localhost:5174"])
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// H6 — Rate limiting for the anonymous auth surface (login/refresh/forgot/reset),
// which was previously unthrottled and brute-forceable. Fixed window per client
// IP (honouring X-Forwarded-For's first hop behind a reverse proxy). Disabled
// under Testing so the auth-heavy integration suite isn't throttled.
var rateLimitIsTesting = builder.Environment.EnvironmentName == "Testing";
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
    {
        if (rateLimitIsTesting)
            return RateLimitPartition.GetNoLimiter("testing");

        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var key = string.IsNullOrWhiteSpace(forwarded)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : forwarded.Split(',')[0].Trim();

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

var app = builder.Build();

// H4 — Treasury is a phased integration, so an unconfigured Treasury does not
// block startup (the webhook fail-closes and initiation throws at call time).
// But surface it loudly instead of silently: warn at boot outside Development
// when the outbound URL or webhook secret are missing.
if (!app.Environment.IsDevelopment())
{
    var treasury = app.Configuration
        .GetSection(CapitalUniversity.Modules.Payments.Abstractions.Treasury.TreasuryOptions.SectionName)
        .Get<CapitalUniversity.Modules.Payments.Abstractions.Treasury.TreasuryOptions>()
        ?? new CapitalUniversity.Modules.Payments.Abstractions.Treasury.TreasuryOptions();
    if (string.IsNullOrWhiteSpace(treasury.BaseUrl) || string.IsNullOrWhiteSpace(treasury.WebhookSecret))
    {
        app.Logger.LogWarning(
            "Treasury integration is not fully configured (Treasury:BaseUrl and/or Treasury:WebhookSecret are empty). " +
            "Payment initiation will fail and inbound Treasury webhooks will be rejected until both are set. " +
            "This is expected ONLY if payments are intentionally disabled for this deployment.");
    }
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

// H7 — Security response headers + HSTS. TLS termination/redirect is owned by the
// reverse proxy in front of this API (the deploy target is out of repo scope), so
// we do not UseHttpsRedirection here (it would loop behind a TLS-terminating
// proxy); HSTS is emitted outside Development to instruct browsers to stay on
// HTTPS. The static headers are safe in every environment.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";
    await next();
});

app.UseExceptionHandler();
app.UseStatusCodePages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var actionExpander = scope.ServiceProvider.GetRequiredService<CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest.ManifestActionExpander>();
    var studentServicesDbContext = scope.ServiceProvider.GetRequiredService<StudentServicesDbContext>();

    // B5 — Schema is managed by EF Core migrations (data-preserving upgrades).
    // CoreDbContext and StudentServicesDbContext share one physical database but
    // each owns a separate migrations-history table — StudentServices uses
    // "__EFMigrationsHistory_StudentServices" in its own schema (see
    // StudentServices/DependencyInjection.cs) — so applying both is collision-free.
    // MigrateAsync creates the database if absent, applies pending migrations,
    // and preserves existing data. Schema creation for the StudentServices.*
    // namespace is handled by EnsureSchema inside its initial migration.
    // Guarded by IsRelational() so the in-memory provider used in tests skips it.
    var autoMigrate = builder.Configuration.GetValue("Database:AutoMigrate", true);
    var isTesting = builder.Environment.EnvironmentName == "Testing";
    // CoreDbContext is normally in-memory under Testing (IsRelational == false →
    // skipped). The benchmark test re-points it at the shared SQL database, which
    // is EnsureCreated-provisioned without a migrations-history row — so use the
    // idempotent EnsureCreated there too; real deployments apply migrations.
    if (autoMigrate && db.Database.IsRelational())
    {
        if (isTesting)
            await db.Database.EnsureCreatedAsync();
        else
            await db.Database.MigrateAsync();
    }

    // The Testing host points StudentServicesDbContext at the shared SQL database
    // (CoreDbContext is swapped to in-memory by the test fixtures, but the
    // StudentServices context is not). That database is provisioned by earlier
    // runs WITHOUT a migrations-history row, so MigrateAsync would try to
    // re-CREATE existing tables. Use idempotent EnsureCreated there (matches the
    // pre-migration self-provisioning behavior); real deployments use migrations.
    if (autoMigrate && studentServicesDbContext.Database.IsRelational())
    {
        if (isTesting)
            await studentServicesDbContext.Database.EnsureCreatedAsync();
        else
            await studentServicesDbContext.Database.MigrateAsync();
    }

    // Reconcile manifest-declared permissions against the DB first, so that
    // SeedRolePermissionsAsync can reference all resources including those
    // declared only in manifests (not in the one-shot SeedAuthResourcesAsync).
    var manifestSync = scope.ServiceProvider
        .GetRequiredService<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifestSynchronizer>();
    await manifestSync.SynchronizeAsync();

    // NOTE: UniversityStructureSeeder (an Engineering/Business node tree) is
    // intentionally NOT called here. DataSeeder.SeedAsync owns the canonical
    // university structure (Capital University → Home Economics / Mataria /
    // Helwan) via its one-shot StructureNodes step, and every downstream
    // seeder (staff FAC-001, students, roles, MassiveDataSeeder programs)
    // resolves node names against THAT tree. Running UniversityStructureSeeder
    // first populated StructureNodes with an incompatible tree, which caused
    // DataSeeder's one-shot structure step to skip, FAC-001 to be dropped, and
    // SeedNotificationsAsync to throw "Staff 'FAC-001' not found" — crashing
    // startup on every clean database.
    // B3 — Demo data (built-in accounts with documented passwords + the bulk
    // MassiveDataSeeder roster) seeds everywhere EXCEPT Production, unless
    // explicitly toggled via Seeding:DemoData. Platform + reference data always
    // seed (see DataSeeder.SeedAsync). Tests run under "Testing" (not Production),
    // so they keep the demo accounts they depend on.
    var seedDemoData = builder.Configuration.GetValue("Seeding:DemoData", !app.Environment.IsProduction());

    await DataSeeder.SeedAsync(db, passwordHasher, actionExpander, seedDemoData);
    //await IdentitySeeder.SeedUsersAsync(db, passwordHasher);

    // Bootstrap a config-driven Super Admin for environments without demo data
    // (no-ops if a Super Admin already exists or if Seeding:Admin:* is unset).
    await ProductionAdminSeeder.SeedAsync(db, passwordHasher, builder.Configuration);

    if (seedDemoData)
    {
        await StudentServicesSeeder.SeedAsync(scope.ServiceProvider);
        await MassiveDataSeeder.SeedAsync(db, passwordHasher);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<SessionVersionMiddleware>();
// M14 — Preload UserScope before any authorisation handler or controller can
// touch its synchronous accessors; eliminates the sync-over-async hot path.
app.UseMiddleware<UserScopePreloadMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

// Health endpoint (anonymous).
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapHub<StudentServicesHub>("/hubs/student-services");

app.MapControllers();
await app.RunAsync();

// Required by WebApplicationFactory<TEntryPoint> in integration tests.
public static partial class Program { }
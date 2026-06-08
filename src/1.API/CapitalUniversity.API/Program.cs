using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Infrastructure;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Persistence.Seeders;
using CapitalUniversity.Modules.AcademicRecords;
using CapitalUniversity.Modules.CourseOffering;
using CapitalUniversity.Modules.Payments;
using CapitalUniversity.Modules.Registration;
using CapitalUniversity.Modules.Schedule;
using CapitalUniversity.Modules.Student;
using CapitalUniversity.Modules.StudentServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Configure JwtSettings & Options
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();

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
builder.Services.AddStudentServicesModule();

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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var actionExpander = scope.ServiceProvider.GetRequiredService<CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest.ManifestActionExpander>();

    // Apply pending EF migrations on startup when running against a relational
    // provider (skipped for InMemory which uses EnsureCreated in tests). Lets the
    // app pick up schema changes shipped in the same release without an explicit
    // "dotnet ef database update" step. Disable by setting
    // "Database:AutoMigrate" = false in appsettings if you prefer explicit gating.
    var autoMigrate = builder.Configuration.GetValue("Database:AutoMigrate", true);
    if (autoMigrate && db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }

    await DataSeeder.SeedAsync(db, passwordHasher, actionExpander);
    await UniversityStructureSeeder.SeedAsync(db);
    await IdentitySeeder.SeedAsync(db);

    // Reconcile manifest-declared permissions against the DB. Additive only —
    // every module owns its permissions through IPermissionManifest, and the
    // synchroniser fills in any missing Module/Service rows without touching
    // teammate-seeded ones. Safe on every startup.
    var manifestSync = scope.ServiceProvider
        .GetRequiredService<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifestSynchronizer>();
    await manifestSync.SynchronizeAsync();
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

// Health endpoint (anonymous).
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapControllers();
await app.RunAsync();

// Required by WebApplicationFactory<TEntryPoint> in integration tests.
public static partial class Program { }
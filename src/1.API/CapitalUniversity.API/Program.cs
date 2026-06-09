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
using CapitalUniversity.Modules.Payments.Persistence;
using CapitalUniversity.Modules.Schedule;
using CapitalUniversity.Modules.Student;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Data.Common;
using System.Text;

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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173" , "http://localhost:5174"])
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
    var studentServicesDbContext = scope.ServiceProvider.GetRequiredService<StudentServicesDbContext>();

    // Ensure database schema is created. There are no migration files, so
    // EnsureCreatedAsync creates all tables + indexes from the model.
    // If the database was previously touched by MigrateAsync (which creates
    // __EFMigrationsHistory but no app tables), drop that tracking table first
    // so EnsureCreated sees a clean slate.
    var autoMigrate = builder.Configuration.GetValue("Database:AutoMigrate", true);
    if (autoMigrate && db.Database.IsRelational())
    {
        // CanConnect returns false when the database does not exist yet,
        // which is the common case on first startup.  Skip the migration
        // history cleanup in that case — EnsureCreatedAsync below will
        // create both the database and the schema from scratch.
        if (db.Database.CanConnect())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID('__EFMigrationsHistory') IS NOT NULL
                    DROP TABLE __EFMigrationsHistory;
                """);
        }
        await db.Database.EnsureCreatedAsync();

        // Create performance indexes that are defined in entity configurations
        // but may not exist on databases that were created before the index
        // definitions were added. Guarded with OBJECT_ID so they run safely
        // on any existing database.
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('Students') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Students_Email' AND object_id = OBJECT_ID('Students'))
                    CREATE INDEX IX_Students_Email ON Students (Email);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Students_StructureNodeId' AND object_id = OBJECT_ID('Students'))
                    CREATE INDEX IX_Students_StructureNodeId ON Students (StructureNodeId);
            END
            IF OBJECT_ID('Staffs') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Staffs_Email' AND object_id = OBJECT_ID('Staffs'))
                    CREATE INDEX IX_Staffs_Email ON Staffs (Email);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Staffs_StructureNodeId' AND object_id = OBJECT_ID('Staffs'))
                    CREATE INDEX IX_Staffs_StructureNodeId ON Staffs (StructureNodeId);
            END
            """);
    }

    // StudentServicesDbContext uses the same database but a different schema
    // (StudentServices.*).  EnsureCreatedAsync skips when ANY tables exist, so
    // we cannot rely on it for the 2nd context.  Instead we generate the CREATE
    // script from the model and run it, guarded by a schema-scoped table check.
    if (autoMigrate && studentServicesDbContext.Database.IsRelational())
    {
        await studentServicesDbContext.Database.ExecuteSqlRawAsync("""
            IF SCHEMA_ID('StudentServices') IS NULL
                EXEC('CREATE SCHEMA StudentServices');
            """);

        var hasTables = false;
        var conn = studentServicesDbContext.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM sys.tables t
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'StudentServices'
                ) THEN 1 ELSE 0 END
                """;
            hasTables = (int)(await cmd.ExecuteScalarAsync())! == 1;
        }

        if (!hasTables)
        {
            var script = studentServicesDbContext.Database.GenerateCreateScript();
            if (!string.IsNullOrWhiteSpace(script))
            {
                var batches = script.Split(
                    ["\nGO\n", "\nGO\r\n", "\r\nGO\r\n", "\r\nGO\n"],
                    StringSplitOptions.RemoveEmptyEntries);
                foreach (var batch in batches)
                {
                    var trimmed = batch.Trim();
                    if (trimmed.Length > 0)
                        await studentServicesDbContext.Database.ExecuteSqlRawAsync(trimmed);
                }
            }
        }
    }

    await UniversityStructureSeeder.SeedAsync(db);
    await DataSeeder.SeedAsync(db, passwordHasher, actionExpander);
    await IdentitySeeder.SeedAsync(db, passwordHasher);
    await StudentServicesSeeder.SeedAsync(scope.ServiceProvider);
    await PaymentsSeeder.SeedAsync(db);
    await MassiveDataSeeder.SeedAsync(db, passwordHasher);

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

app.MapHub<StudentServicesHub>("/hubs/student-services");

app.MapControllers();
await app.RunAsync();

// Required by WebApplicationFactory<TEntryPoint> in integration tests.
public static partial class Program { }
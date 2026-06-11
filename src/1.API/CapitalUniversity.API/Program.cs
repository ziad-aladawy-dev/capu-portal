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
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (origins is { Length: > 0 })
        {
            policy.WithOrigins(origins)
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin();
        }
        policy.AllowAnyHeader()
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

        // Data repair: course codes are language-neutral plain text, but the
        // old create mapper wrapped them in localized JSON that the
        // upper-casing Course.Code setter mangled to {"AR":…,"EN":…}. Such
        // rows break CodeExistsAsync duplicate detection (it compares the
        // bare code) and can render as raw JSON. Unwrap them back to the bare
        // upper-case code; JSON_VALUE paths are case-sensitive so probe both
        // key casings. Rows whose unwrapped code would collide with the
        // unique Code index are left for an operator to resolve. Guarded and
        // idempotent — once no JSON-shaped codes remain this is a no-op.
        // NB: ExecuteSqlRawAsync runs the string through composite formatting,
        // so the literal '{' in the LIKE pattern must be doubled ('{{').
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('Courses') IS NOT NULL
            BEGIN
                WITH BadCodes AS (
                    SELECT Id, Code,
                           UPPER(LEFT(COALESCE(
                               NULLIF(JSON_VALUE(Code, '$.EN'), ''),
                               NULLIF(JSON_VALUE(Code, '$.en'), ''),
                               NULLIF(JSON_VALUE(Code, '$.AR'), ''),
                               NULLIF(JSON_VALUE(Code, '$.ar'), ''),
                               Code), 32)) AS PlainCode
                    FROM Courses
                    WHERE Code LIKE '{{%' AND ISJSON(Code) = 1
                )
                UPDATE b
                SET b.Code = b.PlainCode
                FROM BadCodes b
                WHERE NOT EXISTS (
                    SELECT 1 FROM Courses c
                    WHERE c.Code = b.PlainCode AND c.Id <> b.Id
                );
            END
            """);

        // CoursePrerequisites — added after the initial schema shipped, so
        // EnsureCreatedAsync (which is a no-op on existing databases) will not
        // create it. Guarded CREATE TABLE mirrors the EF model exactly
        // (CoursePrerequisiteConfiguration): unique edge index + reverse-lookup
        // index + Restrict FKs to the catalog.
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('Courses') IS NOT NULL AND OBJECT_ID('CoursePrerequisites') IS NULL
            BEGIN
                CREATE TABLE CoursePrerequisites (
                    Id                   uniqueidentifier NOT NULL PRIMARY KEY,
                    CourseId             uniqueidentifier NOT NULL,
                    PrerequisiteCourseId uniqueidentifier NOT NULL,
                    CreatedAt            datetime2        NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt            datetime2        NULL,
                    IsDeleted            bit              NOT NULL DEFAULT 0,
                    CONSTRAINT FK_CoursePrerequisites_Courses_CourseId
                        FOREIGN KEY (CourseId) REFERENCES Courses (Id),
                    CONSTRAINT FK_CoursePrerequisites_Courses_PrerequisiteCourseId
                        FOREIGN KEY (PrerequisiteCourseId) REFERENCES Courses (Id)
                );
                CREATE UNIQUE INDEX IX_CoursePrerequisites_CourseId_PrerequisiteCourseId
                    ON CoursePrerequisites (CourseId, PrerequisiteCourseId);
                CREATE INDEX IX_CoursePrerequisites_PrerequisiteCourseId
                    ON CoursePrerequisites (PrerequisiteCourseId);
            END
            """);

        // CourseOfferings.InstructorId — column added after the initial schema
        // shipped (EnsureCreatedAsync is a no-op on existing databases).
        // Mirrors CourseOfferingConfiguration: nullable loose pointer, filtered
        // index, deliberately no FK to Staffs. The CREATE INDEX is wrapped in
        // EXEC so it compiles AFTER the ALTER TABLE ran — referencing a column
        // added earlier in the same batch is a compile-time error otherwise.
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('CourseOfferings') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CourseOfferings') AND name = 'InstructorId')
                    ALTER TABLE CourseOfferings ADD InstructorId uniqueidentifier NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CourseOfferings_InstructorId' AND object_id = OBJECT_ID('CourseOfferings'))
                    EXEC('CREATE INDEX IX_CourseOfferings_InstructorId ON CourseOfferings (InstructorId) WHERE InstructorId IS NOT NULL');
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

        // Create the full StudentServices model when its anchor table is absent.
        // EnsureCreatedAsync cannot do this (no-op once the DATABASE exists), and
        // this must run BEFORE the idempotent patch block below: the patch creates
        // a subset of tables (StudentRequests + dependents), which used to satisfy
        // a schema-level "any tables?" probe and skip the full create script on
        // fresh databases — leaving Services/Workflows missing and crashing
        // StudentServicesSeeder at startup.
        var hasServicesTable = false;
        var ssConn = studentServicesDbContext.Database.GetDbConnection();
        if (ssConn.State != ConnectionState.Open)
            await ssConn.OpenAsync();
        await using (var ssCmd = ssConn.CreateCommand())
        {
            ssCmd.CommandText = "SELECT CASE WHEN OBJECT_ID('StudentServices.Services') IS NULL THEN 0 ELSE 1 END";
            hasServicesTable = (int)(await ssCmd.ExecuteScalarAsync())! == 1;
        }

        if (!hasServicesTable)
        {
            // A half-initialized schema (request tables without Services) can be
            // left behind by the old startup ordering; the create script below
            // recreates those tables, so clear them first. Without Services no
            // request rows can be meaningful, so dropping them is safe.
            await studentServicesDbContext.Database.ExecuteSqlRawAsync("""
                DROP TABLE IF EXISTS StudentServices.RequestAttachments;
                DROP TABLE IF EXISTS StudentServices.RequestHistoryEntries;
                DROP TABLE IF EXISTS StudentServices.StudentRequests;
                DROP TABLE IF EXISTS StudentServices.ServiceStructureNodes;
                DROP TABLE IF EXISTS StudentServices.WorkflowStepFields;
                DROP TABLE IF EXISTS StudentServices.WorkflowSteps;
                DROP TABLE IF EXISTS StudentServices.Workflows;
                """);

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

        // Apply pending column additions from migrations — run idempotent ALTER TABLE
        // for each column that may be missing (migration AddFormFieldsToService).
        await studentServicesDbContext.Database.ExecuteSqlRawAsync("""
            ---- Services ------------------------------------------------------------------
            IF OBJECT_ID('StudentServices.Services') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudentServices.Services') AND name = 'LevelOrder')
                    ALTER TABLE StudentServices.Services ADD LevelOrder int NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudentServices.Services') AND name = 'SemesterId')
                    ALTER TABLE StudentServices.Services ADD SemesterId uniqueidentifier NULL;
            END

            ---- StudentRequests + dependents ----------------------------------------------
            -- RequestNumber requires IDENTITY, which can't be added via ALTER TABLE. In
            -- development we drop & recreate the table so EF Core's UseIdentityColumn works.
            -- Dependent tables are also dropped; the seeder repopulates everything on restart.
            IF OBJECT_ID('StudentServices.StudentRequests') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.columns
                               WHERE object_id = OBJECT_ID('StudentServices.StudentRequests')
                                 AND name = 'RequestNumber')
            BEGIN
                DROP TABLE IF EXISTS StudentServices.RequestAttachments;
                DROP TABLE IF EXISTS StudentServices.RequestHistoryEntries;
                DROP TABLE StudentServices.StudentRequests;
            END

            -- If the table was dropped above, recreate it here; otherwise this is a no-op.
            IF OBJECT_ID('StudentServices.StudentRequests') IS NULL
            BEGIN
                CREATE TABLE StudentServices.StudentRequests (
                    Id                  uniqueidentifier  NOT NULL PRIMARY KEY,
                    StudentId           uniqueidentifier  NOT NULL,
                    ServiceId           uniqueidentifier  NOT NULL,
                    RequestNumber       int               NOT NULL IDENTITY(1,1),
                    Status              int               NOT NULL DEFAULT 0,
                    PaymentStatus       int               NOT NULL DEFAULT 0,
                    AmountPaid          decimal(18,2)     NULL,
                    PaymentTransactionId nvarchar(max)     NULL,
                    SubmittedData       nvarchar(max)      NOT NULL,
                    CurrentStepOrder    int               NOT NULL DEFAULT 0,
                    SubmittedAt         datetime2          NULL,
                    CompletedAt         datetime2          NULL,
                    AssignedToStaffId   uniqueidentifier   NULL,
                    AssignedAt          datetime2          NULL,
                    RowVersion          rowversion         NULL,
                    CreatedAt           datetime2          NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt           datetime2          NULL,
                    IsDeleted           bit               NOT NULL DEFAULT 0
                );

                CREATE INDEX IX_StudentRequests_StudentId       ON StudentServices.StudentRequests(StudentId);
                CREATE INDEX IX_StudentRequests_ServiceId       ON StudentServices.StudentRequests(ServiceId);
                CREATE INDEX IX_StudentRequests_Status          ON StudentServices.StudentRequests(Status);
                CREATE INDEX IX_StudentRequests_AssignedToStaffId ON StudentServices.StudentRequests(AssignedToStaffId);
            END

            ---- RequestAttachments --------------------------------------------------------
            IF OBJECT_ID('StudentServices.RequestAttachments') IS NULL
            BEGIN
                CREATE TABLE StudentServices.RequestAttachments (
                    Id                uniqueidentifier  NOT NULL PRIMARY KEY,
                    StudentRequestId  uniqueidentifier  NOT NULL,
                    StepKey           nvarchar(100)     NOT NULL,
                    FileName          nvarchar(500)     NOT NULL,
                    FilePath          nvarchar(1000)    NOT NULL,
                    FileSize          bigint            NOT NULL,
                    MimeType          nvarchar(200)     NOT NULL,
                    CreatedAt         datetime2         NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt         datetime2         NULL,
                    IsDeleted         bit               NOT NULL DEFAULT 0
                );

                CREATE INDEX IX_RequestAttachments_StudentRequestId
                    ON StudentServices.RequestAttachments(StudentRequestId);

                ALTER TABLE StudentServices.RequestAttachments
                    ADD CONSTRAINT FK_RequestAttachments_StudentRequests
                    FOREIGN KEY (StudentRequestId) REFERENCES StudentServices.StudentRequests(Id)
                    ON DELETE CASCADE;
            END

            ---- RequestHistoryEntries -----------------------------------------------------
            IF OBJECT_ID('StudentServices.RequestHistoryEntries') IS NULL
            BEGIN
                CREATE TABLE StudentServices.RequestHistoryEntries (
                    Id                uniqueidentifier  NOT NULL PRIMARY KEY,
                    StudentRequestId  uniqueidentifier  NOT NULL,
                    Action            nvarchar(100)     NOT NULL,
                    Comment           nvarchar(2000)    NULL,
                    PerformedByUserId uniqueidentifier  NULL,
                    PerformedByRole   nvarchar(50)      NULL,
                    PerformedAt       datetime2         NOT NULL DEFAULT GETUTCDATE(),
                    CreatedAt         datetime2         NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt         datetime2         NULL,
                    IsDeleted         bit               NOT NULL DEFAULT 0
                );

                CREATE INDEX IX_RequestHistoryEntries_StudentRequestId
                    ON StudentServices.RequestHistoryEntries(StudentRequestId);

                ALTER TABLE StudentServices.RequestHistoryEntries
                    ADD CONSTRAINT FK_RequestHistoryEntries_StudentRequests
                    FOREIGN KEY (StudentRequestId) REFERENCES StudentServices.StudentRequests(Id)
                    ON DELETE CASCADE;
            END
            """);
    }

    // Reconcile manifest-declared permissions against the DB first, so that
    // SeedRolePermissionsAsync can reference all resources including those
    // declared only in manifests (not in the one-shot SeedAuthResourcesAsync).
    var manifestSync = scope.ServiceProvider
        .GetRequiredService<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifestSynchronizer>();
    await manifestSync.SynchronizeAsync();

    await UniversityStructureSeeder.SeedAsync(db);
    await DataSeeder.SeedAsync(db, passwordHasher, actionExpander);
    await IdentitySeeder.SeedUsersAsync(db, passwordHasher);
    await StudentServicesSeeder.SeedAsync(scope.ServiceProvider);
    await MassiveDataSeeder.SeedAsync(db, passwordHasher, scope.ServiceProvider);
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
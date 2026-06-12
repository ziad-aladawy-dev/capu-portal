using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Host.Admin;
using CapitalUniversity.Sync.Host.Configuration;
using CapitalUniversity.Sync.Host.Scheduling;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.DependencyInjection;
using CapitalUniversity.Sync.Infrastructure.Alerting;
using CapitalUniversity.Sync.Infrastructure.Observability;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.DependencyInjection;
using CapitalUniversity.Sync.Courses.DependencyInjection;
using CapitalUniversity.Sync.Courses.Domain;
using CapitalUniversity.Sync.Courses.Persistence;
using CapitalUniversity.Sync.Courses.Push;
using CapitalUniversity.Sync.Courses.Sources;
using CapitalUniversity.Sync.Schedules.DependencyInjection;
using CapitalUniversity.Sync.Schedules.Domain;
using CapitalUniversity.Sync.Schedules.Persistence;
using CapitalUniversity.Sync.Schedules.Push;
using CapitalUniversity.Sync.Schedules.Sources;
using CapitalUniversity.Sync.Registration.DependencyInjection;
using CapitalUniversity.Modules.Registration.Domain;
// Enums used by the admin-seed endpoints now live on the operational sides
// (Core / module abstractions) since the sync layer no longer duplicates them.
using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Infrastructure.Logging;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Outbox;
using CapitalUniversity.Core.Infrastructure.Sync;
using CapitalUniversity.Modules.CourseOffering.Domain;
using CapitalUniversity.Modules.Payments;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Sync.Staff.DependencyInjection;
using CapitalUniversity.Sync.Staff.Domain;
using CapitalUniversity.Sync.Staff.Persistence;
using CapitalUniversity.Sync.Staff.Push;
using CapitalUniversity.Sync.Staff.Sources;
using CapitalUniversity.Sync.Student.DependencyInjection;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Persistence;
using CapitalUniversity.Sync.Student.Push;
using CapitalUniversity.Sync.Student.Sources;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSyncInfrastructure();
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));
builder.Services.Configure<SyncRetentionOptions>(
    builder.Configuration.GetSection(SyncRetentionOptions.SectionName));
builder.Services.Configure<CapitalUniversity.Sync.Infrastructure.Scheduling.SyncOrphanReaperOptions>(
    builder.Configuration.GetSection(
        CapitalUniversity.Sync.Infrastructure.Scheduling.SyncOrphanReaperOptions.SectionName));
builder.Services.Configure<SyncHangfireOptions>(
    builder.Configuration.GetSection($"{SyncOptions.SectionName}:Hangfire"));

var syncOptions = builder.Configuration.GetSection(SyncOptions.SectionName).Get<SyncOptions>()
    ?? new SyncOptions();

if (string.IsNullOrWhiteSpace(syncOptions.Hangfire.ConnectionString))
{
    throw new InvalidOperationException(
        "Sync:Hangfire:ConnectionString is required. Configure it in appsettings or environment.");
}

builder.Services.AddSyncPersistence(syncOptions.Hangfire.ConnectionString);

// ── CoreDbContext + ICoreWriteGateway ────────────────────────────────────────
// Sync writes to Core's operational tables exclusively through CoreWriteGateway.
// The host wires CoreDbContext directly (without AddCoreServices, which would
// pull Mongo / Redis / Identity that the sync service has no use for) and
// registers the gateway as Scoped to share the request-scoped DbContext.
//
// Each module entity (Invoice, ScheduleSlot, CourseOffering) lives in its own
// project; their EF configurations are picked up through CoreDbContext's
// module-assembly registration list. Must run BEFORE Build() so the static list
// is populated before the first context instantiation.
var coreConnectionString = builder.Configuration["Sync:Core:ConnectionString"];
if (string.IsNullOrWhiteSpace(coreConnectionString))
{
    throw new InvalidOperationException(
        "Sync:Core:ConnectionString is required — sync writes to Core through CoreDbContext.");
}

CoreDbContext.ModuleConfigurationAssemblies.Add(typeof(CapitalUniversity.Modules.Payments.Domain.Treasury.TreasuryReceipt).Assembly);
CoreDbContext.ModuleConfigurationAssemblies.Add(typeof(ScheduleSlot).Assembly);
CoreDbContext.ModuleConfigurationAssemblies.Add(typeof(CourseOffering).Assembly);
// Registration read-model lives in Core's StudentRegisteredCourses table; the
// gateway needs its EF configuration to upsert synced rows.
CoreDbContext.ModuleConfigurationAssemblies.Add(typeof(StudentRegisteredCourse).Assembly);

builder.Services.AddDbContext<CoreDbContext>(opts => opts.UseSqlServer(coreConnectionString));
builder.Services.AddScoped<ICoreWriteGateway, CoreWriteGateway>();

// ── Mongo audit trail ─────────────────────────────────────────────────────────
// Wire the Mongo audit pipeline so CoreDbContext's EF auto-trail records every
// write the sync platform makes into Core tables (courses, schedules, finance,
// staff, student). Sync jobs run without an HttpContext, so these entries are
// recorded as system actions (no user/IP) — which is correct for the platform.
// The ISyncLogger decorator additionally mirrors sync warnings/errors into Mongo.
builder.Services.AddMongoAuditLogging(builder.Configuration);
builder.Services.AddSingleton<CapitalUniversity.Sync.Infrastructure.Observability.SyncLogger>();
builder.Services.AddSingleton<ISyncLogger>(sp =>
    new CapitalUniversity.Sync.Host.Observability.MongoAuditingSyncLogger(
        sp.GetRequiredService<CapitalUniversity.Sync.Infrastructure.Observability.SyncLogger>(),
        sp.GetRequiredService<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger>()));

// Notifies everyone who can access the sync layer (holders of the "sync"
// permission) when a run completes or terminally fails. Scoped — shares the
// request/job-scoped CoreDbContext. The executor and dead-letter filter resolve
// it through a fresh scope; this is the only place it is wired to Core.
builder.Services.AddScoped<
    CapitalUniversity.Sync.Abstractions.Contracts.ISyncOutcomeNotifier,
    CapitalUniversity.Sync.Host.Notifications.CoreSyncOutcomeNotifier>();

builder.Services.AddStudentSync(builder.Configuration);
builder.Services.AddStaffSync(builder.Configuration);
builder.Services.AddCoursesSync(builder.Configuration);
builder.Services.AddSchedulesSync(builder.Configuration);
// Pull-only: registrations flow in from the external academic system and are
// never modified locally, so there is no push/outbox/DbContext to wire.
builder.Services.AddRegistrationSync(builder.Configuration);

// HTTP-adapter override. When Sync:Integration:UseHttpAdapters is true, HTTP
// implementations replace the per-module in-memory ones via DI last-wins.
builder.Services.AddSyncHttpAdaptersIfEnabled(builder.Configuration);

// Treasury receipt synchronization (Phase 3). Outbound client + receipt sync
// service + Hangfire trigger. Receipts merge into Core via ICoreWriteGateway
// (already registered above). Additive — does not touch existing sync modules.
builder.Services.AddTreasuryIntegration(builder.Configuration);
builder.Services.AddTreasuryReceiptSync();
builder.Services.AddScoped<CapitalUniversity.Sync.Host.Scheduling.TreasuryReceiptPullTrigger>();

// Treasury settlement + reconciliation (Phase 7). Wire the transactional outbox
// into this host so reconciliation-driven settlements emit FeePaidEvent exactly
// like the webhook path. Rows are staged on the shared CoreDbContext and drained
// by the API host's OutboxDispatcher (same database).
builder.Services.AddHttpContextAccessor();
builder.Services.AddOutboxForBackgroundHost();
builder.Services.AddTreasuryReconciliation();
builder.Services.AddScoped<CapitalUniversity.Sync.Host.Scheduling.TreasuryReconciliationTrigger>();

builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(syncOptions.Hangfire.ConnectionString, new SqlServerStorageOptions
    {
        SchemaName = syncOptions.Hangfire.SchemaName,
        PrepareSchemaIfNecessary = syncOptions.Hangfire.PrepareSchemaIfNecessary,
        QueuePollInterval = syncOptions.Hangfire.QueuePollInterval,
        CommandBatchMaxTimeout = syncOptions.Hangfire.CommandBatchMaxTimeout,
        SlidingInvisibilityTimeout = syncOptions.Hangfire.SlidingInvisibilityTimeout,
        JobExpirationCheckInterval = syncOptions.Hangfire.JobExpirationCheckInterval,
        DisableGlobalLocks = true
    }));

// Per-queue worker pools. When Sync:Hangfire:QueuePools is configured, register
// ONE BackgroundJobServer per pool — each owns a disjoint set of queues with
// its own WorkerCount. When QueuePools is empty, fall back to a single server
// listening on all of Sync:Hangfire:Queues with one shared WorkerCount.
if (syncOptions.Hangfire.QueuePools.Count > 0)
{
    foreach (var pool in syncOptions.Hangfire.QueuePools)
    {
        var capturedPool = pool;
        builder.Services.AddHangfireServer(options =>
        {
            options.ServerName = $"sync-host:{Environment.MachineName}:" +
                                 (capturedPool.Name ?? string.Join(",", capturedPool.Queues));
            options.WorkerCount = capturedPool.WorkerCount;
            options.Queues = capturedPool.Queues.ToArray();
            options.CancellationCheckInterval = syncOptions.Hangfire.ServerCancellationCheckInterval;
        });
    }
}
else
{
    builder.Services.AddHangfireServer(options =>
    {
        options.ServerName = $"sync-host:{Environment.MachineName}";
        options.WorkerCount = syncOptions.Hangfire.WorkerCount
            ?? Math.Max(1, Environment.ProcessorCount);
        options.Queues = syncOptions.Hangfire.Queues.Count > 0
            ? syncOptions.Hangfire.Queues.ToArray()
            : new[] { "default" };

        options.CancellationCheckInterval = syncOptions.Hangfire.ServerCancellationCheckInterval;
    });
}

builder.Services.AddSingleton<SyncRecurringTrigger>();
builder.Services.AddHostedService<SyncRecurringJobsRegistrar>();

builder.Services.AddSingleton<SyncDeadLetterFilter>();

// ── Authentication & authorization ─────────────────────────────────────────────
// The sync host shares JWT bearer trust with the API. Operators log in against
// the API, receive a JWT with their staff role claim, and reuse the same token
// here — there is no second credential store, no second login flow. The
// `SyncAdmin` policy below is the single gate every admin surface (/admin/*
// endpoints AND the Hangfire dashboard) goes through.
//
// SyncAuthOptions.DevAllowAnonymous is a dev-only escape hatch (off by
// default). Production NEVER bypasses the policy.
builder.Services.Configure<SyncAuthOptions>(
    builder.Configuration.GetSection(SyncAuthOptions.SectionName));
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? new JwtSettings();
var syncAuthOptions = builder.Configuration.GetSection(SyncAuthOptions.SectionName).Get<SyncAuthOptions>()
    ?? new SyncAuthOptions();

const string JwtDevPlaceholderKey = "YourSuperSecretKeyAtLeast32CharactersLong!";
if (!builder.Environment.IsDevelopment()
    && (string.IsNullOrWhiteSpace(jwtSettings?.Key) || jwtSettings.Key == JwtDevPlaceholderKey))
{
    throw new InvalidOperationException(
        "Jwt:Key is unset or is the built-in development placeholder. Supply a unique " +
        "secret of at least 32 characters via environment variable or secret store " +
        "(e.g. Jwt__Key) before running outside the Development environment.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Key ?? string.Empty)),
            // The API issues role claims via the JWT `role` claim type
            // (Microsoft's ClaimTypes.Role default). RequireRole below picks
            // that up directly.
        };
    });

// The SyncAdmin policy is the only thing the admin surface checks. It is
// identical across every environment — there is no IsDevelopment() bypass.
// Local devs authenticate the same way operators do in production: log into
// the API, reuse the JWT here.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SyncAuthPolicies.SyncAdmin, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(syncAuthOptions.RequiredRole);
    });
});

// Health checks — Hangfire/audit DB and the per-module DBs each get their own probe.
var studentConn = builder.Configuration["Sync:Student:ConnectionString"] ?? "";
var staffConn = builder.Configuration["Sync:Staff:ConnectionString"] ?? "";
var coursesConn = builder.Configuration["Sync:Courses:ConnectionString"] ?? "";
var schedulesConn = builder.Configuration["Sync:Schedules:ConnectionString"] ?? "";
builder.Services.AddHealthChecks()
    .AddCheck("hangfire-sql", new SqlConnectivityHealthCheck(
        syncOptions.Hangfire.ConnectionString, "Hangfire + sync audit DB"))
    .AddCheck("student-db", new SqlConnectivityHealthCheck(studentConn, "Sync.Student DB"))
    .AddCheck("staff-db", new SqlConnectivityHealthCheck(staffConn, "Sync.Staff DB"))
    .AddCheck("courses-db", new SqlConnectivityHealthCheck(coursesConn, "Sync.Courses DB"))
    .AddCheck("schedules-db", new SqlConnectivityHealthCheck(schedulesConn, "Sync.Schedules DB"));

var app = builder.Build();

using (var migrationScope = app.Services.CreateScope())
{
    var db = migrationScope.ServiceProvider.GetRequiredService<SyncDbContext>();
    await db.Database.MigrateAsync();

    // Sync DbContexts now own only outbox tables (operational rows live in
    // Core and are written through ICoreWriteGateway). Each module has a
    // freshly-generated outbox-only Initial migration; we apply them
    // declaratively. CoreDbContext is NOT migrated by the sync host — Core
    // owns its own migration cadence and the sync host is not the writer of
    // record for those schemas.
    var studentDb = migrationScope.ServiceProvider.GetRequiredService<StudentSyncDbContext>();
    await studentDb.Database.MigrateAsync();

    var staffDb = migrationScope.ServiceProvider.GetRequiredService<StaffSyncDbContext>();
    await staffDb.Database.MigrateAsync();

    var coursesDb = migrationScope.ServiceProvider.GetRequiredService<CoursesSyncDbContext>();
    await coursesDb.Database.MigrateAsync();

    var schedulesDb = migrationScope.ServiceProvider.GetRequiredService<SchedulesSyncDbContext>();
    await schedulesDb.Database.MigrateAsync();
}

GlobalJobFilters.Filters.Add(app.Services.GetRequiredService<SyncDeadLetterFilter>());

app.UseAuthentication();
app.UseAuthorization();

// ── Dashboard auth ─────────────────────────────────────────────────────────────
// Sync:ExposeAdminEndpoints is the kill-switch (operators turning it off
// removes the dashboard from the routing table entirely). When it's on, the
// dashboard ALWAYS requires the SyncAdmin role — there is no
// environment-coupled bypass. The dev-anonymous escape hatch has been retired
// (audit P0-2): local devs authenticate the same way operators do in prod
// (log into the API, reuse the JWT here).
if (syncOptions.ExposeAdminEndpoints)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new IDashboardAuthorizationFilter[]
        {
            new RoleBasedDashboardAuthorizationFilter(syncAuthOptions.RequiredRole)
        },
        DisplayStorageConnectionString = false,
        DashboardTitle = "CapU Sync — Hangfire"
    });
}

app.MapGet("/", () =>
{
    var opts = app.Services.GetRequiredService<IOptions<SyncOptions>>().Value;
    return Results.Ok(new
    {
        service = "CapitalUniversity.Sync.Host",
        dashboard = "/hangfire",
        storage = "SqlServer",
        hangfireSchema = opts.Hangfire.SchemaName,
        syncSchema = SyncDbContext.SchemaName,
        queues = opts.Hangfire.Queues,
        admin = new
        {
            trigger = "POST /admin/trigger/{module}?direction=Pull|Push"
        }
    });
});

app.MapGet("/healthz", () => Results.Ok("healthy"));

app.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception?.Message
            })
        });
        await context.Response.WriteAsync(payload);
    }
});

// ── Admin endpoints ────────────────────────────────────────────────────────────
// Sync:ExposeAdminEndpoints is the kill-switch (turning it off drops every
// admin route from the routing table). When on, every endpoint goes through
// the SyncAdmin authorization policy — anonymous callers get 401, callers
// without the role get 403. The dev escape hatch lives entirely in the policy
// definition (see Sync:Auth:DevAllowAnonymous wiring above), so the same code
// path executes in dev and prod; only the requirement inside the policy
// differs.
if (syncOptions.ExposeAdminEndpoints)
{
    var admin = app.MapGroup("/admin").RequireAuthorization(SyncAuthPolicies.SyncAdmin);

    // Per-queue lag observability. Read-only over Hangfire SQL storage.
    admin.MapGet("/queues/lag", async (QueueLagProbe probe, CancellationToken ct) =>
    {
        var snapshots = await probe.SampleAsync(ct);
        return Results.Ok(new
        {
            sampledAt = DateTimeOffset.UtcNow,
            queues = snapshots.Select(s => new
            {
                queue = s.Queue,
                enqueued = s.EnqueuedCount,
                processing = s.ProcessingCount,
                oldestEnqueuedAt = s.OldestEnqueuedAt,
                oldestAgeSeconds = s.OldestAge?.TotalSeconds
            })
        });
    });

    admin.MapGet("/retention", (IOptions<SyncRetentionOptions> opts) =>
    {
        var o = opts.Value;
        return Results.Ok(new
        {
            enabled = o.Enabled,
            cron = o.CronExpression,
            windows = new
            {
                successfulRunsDays = o.SuccessfulRunsRetentionDays,
                failedRunsDays = o.FailedRunsRetentionDays,
                failureRowsDays = o.FailureRowsRetentionDays,
                deadLettersDays = o.DeadLettersRetentionDays
            },
            deleteBatchSize = o.DeleteBatchSize,
            maxDeletedPerTablePerRun = o.MaxDeletedPerTablePerRun,
            outboxTables = o.OutboxTables.Select(t => new
            {
                t.Schema, t.Table, t.RetentionDays,
                t.StatusColumn, t.ProcessedStatusValue, t.TimestampColumn
            })
        });
    });

    admin.MapPost("/retention/run", async (
        CapitalUniversity.Sync.Infrastructure.Scheduling.SyncRetentionService svc,
        CancellationToken ct) =>
    {
        await svc.RunAsync(ct);
        return Results.Ok(new { ranAt = DateTimeOffset.UtcNow });
    });

    admin.MapPost("/reaper/run", async (
        CapitalUniversity.Sync.Infrastructure.Scheduling.SyncOrphanReaperService svc,
        CancellationToken ct) =>
    {
        await svc.RunAsync(ct);
        return Results.Ok(new { ranAt = DateTimeOffset.UtcNow });
    });

    // Admin trigger — manual on-demand enqueue.
    admin.MapPost("/trigger/{module}", async (
        string module,
        string? direction,
        ISyncDispatcher dispatcher,
        CancellationToken ct) =>
    {
        var dir = direction?.Equals("Push", StringComparison.OrdinalIgnoreCase) == true
            ? SyncDirection.Push
            : SyncDirection.Pull;

        var metadata = new SyncRunMetadata
        {
            CorrelationId = Guid.NewGuid(),
            TriggeredBy = "admin"
        };

        var jobId = await dispatcher.DispatchAsync(module, dir, metadata, ct);
        return Results.Ok(new
        {
            module,
            direction = dir.ToString(),
            jobId,
            metadata.CorrelationId
        });
    });

    // Forces an immediate execution of the next attempt. Calling N times exhausts
    // the [AutomaticRetry(Attempts=4)] policy.
    admin.MapPost("/requeue/{jobId}", (
        string jobId,
        Hangfire.IBackgroundJobClient client) =>
    {
        var ok = client.Requeue(jobId);
        return Results.Ok(new { jobId, requeued = ok });
    });

    // Outbox seed (Student) — seeds a Pending outbox row for the given ExternalStudentId.
    admin.MapPost("/outbox/student/{externalStudentId}", async (
        string externalStudentId,
        StudentOutboxSeedRequest? body,
        IServiceScopeFactory scopeFactory,
        CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(externalStudentId))
        {
            return Results.BadRequest(new { error = "externalStudentId required." });
        }

        var payload = new ExternalStudent
        {
            ExternalStudentId = externalStudentId,
            StudentCode = body?.StudentCode ?? $"STU-{externalStudentId}",
            Name = body?.Name ?? "Pushed Student",
            NationalId = body?.NationalId ?? $"NID-{externalStudentId}",
            BirthDate = body?.BirthDate ?? new DateTime(2000, 1, 1),
            PhoneNumber = body?.PhoneNumber ?? "+200000000000",
            Email = body?.Email ?? $"{externalStudentId.ToLowerInvariant()}@push.test",
            IsActive = body?.IsActive ?? true,
            ExternalUpdatedAt = body?.ExternalUpdatedAt ?? DateTimeOffset.UtcNow,
            ExternalVersion = body?.ExternalVersion ?? 1
        };

        var row = new StudentOutboxEntity
        {
            ExternalStudentId = externalStudentId,
            Operation = OutboxOperation.Upsert,
            Payload = OutboxPayloadSerializer.Serialize(payload),
            PayloadSchemaVersion = StudentOutboxEntity.CurrentPayloadSchemaVersion,
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentSyncDbContext>();
        db.StudentOutbox.Add(row);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            outboxId = row.Id,
            externalStudentId,
            status = row.Status.ToString(),
            createdAt = row.CreatedAt
        });
    });

    admin.MapGet("/outbox/sink", (InMemoryExternalStudentSink sink) =>
    {
        return Results.Ok(new
        {
            acceptedCount = sink.AcceptedCount,
            accepted = sink.Accepted.Select(kvp => new
            {
                externalStudentId = kvp.Key,
                kvp.Value.StudentCode,
                kvp.Value.Name,
                kvp.Value.NationalId,
                kvp.Value.PhoneNumber,
                kvp.Value.Email,
                kvp.Value.IsActive,
                kvp.Value.ExternalVersion,
                kvp.Value.ExternalUpdatedAt
            })
        });
    });

    admin.MapPost("/outbox/sink/fail-next/{externalStudentId}", (
        string externalStudentId,
        InMemoryExternalStudentSink sink) =>
    {
        sink.FailNextPushFor(externalStudentId);
        return Results.Ok(new { externalStudentId, armed = true });
    });

    // Outbox seed (Staff) — mirror of Student.
    admin.MapPost("/outbox/staff/{externalStaffId}", async (
        string externalStaffId,
        StaffOutboxSeedRequest? body,
        IServiceScopeFactory scopeFactory,
        CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(externalStaffId))
        {
            return Results.BadRequest(new { error = "externalStaffId required." });
        }

        var payload = new ExternalStaff
        {
            ExternalStaffId = externalStaffId,
            EmployeeCode = body?.EmployeeCode ?? $"EMP-{externalStaffId}",
            Name = body?.Name ?? "Pushed Staff",
            NationalId = body?.NationalId ?? $"NID-T-{externalStaffId}",
            BirthDate = body?.BirthDate ?? new DateTime(1985, 1, 1),
            PhoneNumber = body?.PhoneNumber ?? "+200000000000",
            Email = body?.Email ?? $"{externalStaffId.ToLowerInvariant()}@push.test",
            Role = body?.Role ?? "instructor",
            JobTitle = body?.JobTitle ?? "Lecturer",
            IsActive = body?.IsActive ?? true,
            ExternalUpdatedAt = body?.ExternalUpdatedAt ?? DateTimeOffset.UtcNow,
            ExternalVersion = body?.ExternalVersion ?? 1
        };

        var row = new StaffOutboxEntity
        {
            ExternalStaffId = externalStaffId,
            Operation = OutboxOperation.Upsert,
            Payload = OutboxPayloadSerializer.Serialize(payload),
            PayloadSchemaVersion = StaffOutboxEntity.CurrentPayloadSchemaVersion,
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StaffSyncDbContext>();
        db.StaffOutbox.Add(row);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { outboxId = row.Id, externalStaffId, status = row.Status.ToString(), createdAt = row.CreatedAt });
    });

    admin.MapGet("/outbox/staff/sink", (InMemoryExternalStaffSink sink) =>
    {
        return Results.Ok(new
        {
            acceptedCount = sink.AcceptedCount,
            accepted = sink.Accepted.Select(kvp => new
            {
                externalStaffId = kvp.Key,
                kvp.Value.EmployeeCode,
                kvp.Value.Name,
                kvp.Value.NationalId,
                kvp.Value.PhoneNumber,
                kvp.Value.Email,
                kvp.Value.Role,
                kvp.Value.JobTitle,
                kvp.Value.IsActive,
                kvp.Value.ExternalVersion,
                kvp.Value.ExternalUpdatedAt
            })
        });
    });

    admin.MapPost("/outbox/staff/sink/fail-next/{externalStaffId}", (
        string externalStaffId,
        InMemoryExternalStaffSink sink) =>
    {
        sink.FailNextPushFor(externalStaffId);
        return Results.Ok(new { externalStaffId, armed = true });
    });

    // Outbox seed (Courses) — mirror of Student/Staff.
    admin.MapPost("/outbox/courses/{externalCourseId}", async (
        string externalCourseId,
        CourseOutboxSeedRequest? body,
        IServiceScopeFactory scopeFactory,
        CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(externalCourseId))
        {
            return Results.BadRequest(new { error = "externalCourseId required." });
        }

        var payload = new ExternalCourse
        {
            ExternalCourseId = externalCourseId,
            Code = body?.Code ?? $"PUSH-{externalCourseId}",
            Title = body?.Title ?? "Pushed Course",
            CreditHours = body?.CreditHours ?? 3,
            Category = body?.Category ?? CourseCategory.Elective,
            IsActive = body?.IsActive ?? true,
            ExternalUpdatedAt = body?.ExternalUpdatedAt ?? DateTimeOffset.UtcNow,
            ExternalVersion = body?.ExternalVersion ?? 1
        };

        var row = new CourseOutboxEntity
        {
            ExternalCourseId = externalCourseId,
            Operation = OutboxOperation.Upsert,
            Payload = OutboxPayloadSerializer.Serialize(payload),
            PayloadSchemaVersion = CourseOutboxEntity.CurrentPayloadSchemaVersion,
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoursesSyncDbContext>();
        db.CoursesOutbox.Add(row);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { outboxId = row.Id, externalCourseId, status = row.Status.ToString(), createdAt = row.CreatedAt });
    });

    admin.MapGet("/outbox/courses/sink", (InMemoryExternalCourseSink sink) =>
    {
        return Results.Ok(new
        {
            acceptedCount = sink.AcceptedCount,
            accepted = sink.Accepted.Select(kvp => new
            {
                externalCourseId = kvp.Key,
                kvp.Value.Code,
                kvp.Value.Title,
                kvp.Value.CreditHours,
                category = kvp.Value.Category.ToString(),
                kvp.Value.IsActive,
                kvp.Value.ExternalVersion,
                kvp.Value.ExternalUpdatedAt
            })
        });
    });

    admin.MapPost("/outbox/courses/sink/fail-next/{externalCourseId}", (
        string externalCourseId,
        InMemoryExternalCourseSink sink) =>
    {
        sink.FailNextPushFor(externalCourseId);
        return Results.Ok(new { externalCourseId, armed = true });
    });


    // Outbox seed (Schedules / ScheduleSlot) — mirror of Student/Staff.
    admin.MapPost("/outbox/schedules/{externalScheduleSlotId}", async (
        string externalScheduleSlotId,
        ScheduleSlotOutboxSeedRequest? body,
        IServiceScopeFactory scopeFactory,
        CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(externalScheduleSlotId))
        {
            return Results.BadRequest(new { error = "externalScheduleSlotId required." });
        }

        var payload = new ExternalScheduleSlot
        {
            ExternalScheduleSlotId = externalScheduleSlotId,
            ExternalCourseOfferingId = body?.ExternalCourseOfferingId ?? "EXT-CO-0001",
            DayOfWeek = body?.DayOfWeek ?? DayOfWeek.Monday,
            StartTime = body?.StartTime ?? new TimeOnly(9, 0),
            EndTime = body?.EndTime ?? new TimeOnly(10, 0),
            Kind = body?.Kind ?? ScheduleSlotKind.Lecture,
            Location = body?.Location,
            Notes = body?.Notes,
            ExternalUpdatedAt = body?.ExternalUpdatedAt ?? DateTimeOffset.UtcNow,
            ExternalVersion = body?.ExternalVersion ?? 1
        };

        var row = new ScheduleSlotOutboxEntity
        {
            ExternalScheduleSlotId = externalScheduleSlotId,
            Operation = OutboxOperation.Upsert,
            Payload = OutboxPayloadSerializer.Serialize(payload),
            PayloadSchemaVersion = ScheduleSlotOutboxEntity.CurrentPayloadSchemaVersion,
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulesSyncDbContext>();
        db.ScheduleSlotsOutbox.Add(row);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { outboxId = row.Id, externalScheduleSlotId, status = row.Status.ToString(), createdAt = row.CreatedAt });
    });

    admin.MapGet("/outbox/schedules/sink", (InMemoryExternalScheduleSlotSink sink) =>
    {
        return Results.Ok(new
        {
            acceptedCount = sink.AcceptedCount,
            accepted = sink.Accepted.Select(kvp => new
            {
                externalScheduleSlotId = kvp.Key,
                kvp.Value.ExternalCourseOfferingId,
                dayOfWeek = kvp.Value.DayOfWeek.ToString(),
                startTime = kvp.Value.StartTime.ToString("HH:mm"),
                endTime = kvp.Value.EndTime.ToString("HH:mm"),
                kind = kvp.Value.Kind.ToString(),
                kvp.Value.Location,
                kvp.Value.Notes,
                kvp.Value.ExternalVersion,
                kvp.Value.ExternalUpdatedAt
            })
        });
    });

    admin.MapPost("/outbox/schedules/sink/fail-next/{externalScheduleSlotId}", (
        string externalScheduleSlotId,
        InMemoryExternalScheduleSlotSink sink) =>
    {
        sink.FailNextPushFor(externalScheduleSlotId);
        return Results.Ok(new { externalScheduleSlotId, armed = true });
    });

    // Replay — dispatches a fresh run mirroring the original's (Module, Direction).
    // Records the link via a `ReplayOf` tag. Works regardless of original terminal state.
    admin.MapPost("/replay/{correlationId:guid}", async (
        Guid correlationId,
        IServiceScopeFactory scopeFactory,
        ISyncDispatcher dispatcher,
        CancellationToken ct) =>
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        var original = await db.Runs
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CorrelationId == correlationId, ct);

        if (original is null)
        {
            return Results.NotFound(new { correlationId, error = "Original run not found in sync.runs." });
        }

        var metadata = new SyncRunMetadata
        {
            CorrelationId = Guid.NewGuid(),
            TriggeredBy = "replay",
            Tags = new Dictionary<string, string>
            {
                ["ReplayOf"] = correlationId.ToString(),
                ["ReplayOfStatus"] = original.Status.ToString()
            }
        };

        var jobId = await dispatcher.DispatchAsync(
            original.ModuleName,
            original.Direction,
            metadata,
            ct);

        return Results.Ok(new
        {
            originalCorrelationId = correlationId,
            originalStatus = original.Status.ToString(),
            replayCorrelationId = metadata.CorrelationId,
            module = original.ModuleName,
            direction = original.Direction.ToString(),
            jobId
        });
    });
}

app.Run();

namespace CapitalUniversity.Sync.Host
{
    public partial class Program;
}
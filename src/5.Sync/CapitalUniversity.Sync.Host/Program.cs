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
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.DependencyInjection;
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
builder.Services.AddStudentSync(builder.Configuration);
builder.Services.AddStaffSync(builder.Configuration);

// HTTP-adapter override. When Sync:Integration:UseHttpAdapters is true, HTTP
// implementations replace the per-module in-memory ones via DI last-wins.
builder.Services.AddSyncHttpAdaptersIfEnabled(builder.Configuration);

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

// Health checks — Hangfire/audit DB and the per-module DBs each get their own probe.
var studentConn = builder.Configuration["Sync:Student:ConnectionString"] ?? "";
var staffConn = builder.Configuration["Sync:Staff:ConnectionString"] ?? "";
builder.Services.AddHealthChecks()
    .AddCheck("hangfire-sql", new SqlConnectivityHealthCheck(
        syncOptions.Hangfire.ConnectionString, "Hangfire + sync audit DB"))
    .AddCheck("student-db", new SqlConnectivityHealthCheck(studentConn, "Sync.Student DB"))
    .AddCheck("staff-db", new SqlConnectivityHealthCheck(staffConn, "Sync.Staff DB"));

var app = builder.Build();

using (var migrationScope = app.Services.CreateScope())
{
    var db = migrationScope.ServiceProvider.GetRequiredService<SyncDbContext>();
    await db.Database.MigrateAsync();

    var studentDb = migrationScope.ServiceProvider.GetRequiredService<StudentSyncDbContext>();
    await studentDb.Database.MigrateAsync();

    var staffDb = migrationScope.ServiceProvider.GetRequiredService<StaffSyncDbContext>();
    await staffDb.Database.MigrateAsync();
}

GlobalJobFilters.Filters.Add(app.Services.GetRequiredService<SyncDeadLetterFilter>());

// ── Dashboard auth ─────────────────────────────────────────────────────────────
// Both gates required: IsDevelopment AND Sync:ExposeAdminEndpoints=true. The
// AllowAll filter grants anonymous access; a single env var slip should not be
// enough to expose it. For production, swap with RoleBasedDashboardAuthorizationFilter
// + an upstream auth scheme (cookie / JWT / Windows) that populates HttpContext.User.
if (app.Environment.IsDevelopment() && syncOptions.ExposeAdminEndpoints)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new AllowAllDashboardAuthorizationFilter() },
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
// Development only AND requires Sync:ExposeAdminEndpoints=true. None of these have
// authentication. For production, move the endpoints operators need out of this
// gate AND wire real auth (RequireAuthorization, an API-key middleware, or a
// network policy).
if (app.Environment.IsDevelopment() && syncOptions.ExposeAdminEndpoints)
{
    // Per-queue lag observability. Read-only over Hangfire SQL storage.
    app.MapGet("/admin/queues/lag", async (QueueLagProbe probe, CancellationToken ct) =>
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

    app.MapGet("/admin/retention", (IOptions<SyncRetentionOptions> opts) =>
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

    app.MapPost("/admin/retention/run", async (
        CapitalUniversity.Sync.Infrastructure.Scheduling.SyncRetentionService svc,
        CancellationToken ct) =>
    {
        await svc.RunAsync(ct);
        return Results.Ok(new { ranAt = DateTimeOffset.UtcNow });
    });

    app.MapPost("/admin/reaper/run", async (
        CapitalUniversity.Sync.Infrastructure.Scheduling.SyncOrphanReaperService svc,
        CancellationToken ct) =>
    {
        await svc.RunAsync(ct);
        return Results.Ok(new { ranAt = DateTimeOffset.UtcNow });
    });

    // Admin trigger — manual on-demand enqueue.
    app.MapPost("/admin/trigger/{module}", async (
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
    app.MapPost("/admin/requeue/{jobId}", (
        string jobId,
        Hangfire.IBackgroundJobClient client) =>
    {
        var ok = client.Requeue(jobId);
        return Results.Ok(new { jobId, requeued = ok });
    });

    // Outbox seed (Student) — seeds a Pending outbox row for the given ExternalStudentId.
    app.MapPost("/admin/outbox/student/{externalStudentId}", async (
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
            FirstName = body?.FirstName ?? "PushedFirst",
            LastName = body?.LastName ?? "PushedLast",
            Email = body?.Email ?? $"{externalStudentId.ToLowerInvariant()}@push.test",
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

    app.MapGet("/admin/outbox/sink", (InMemoryExternalStudentSink sink) =>
    {
        return Results.Ok(new
        {
            acceptedCount = sink.AcceptedCount,
            accepted = sink.Accepted.Select(kvp => new
            {
                externalStudentId = kvp.Key,
                kvp.Value.FirstName,
                kvp.Value.LastName,
                kvp.Value.Email,
                kvp.Value.ExternalVersion,
                kvp.Value.ExternalUpdatedAt
            })
        });
    });

    app.MapPost("/admin/outbox/sink/fail-next/{externalStudentId}", (
        string externalStudentId,
        InMemoryExternalStudentSink sink) =>
    {
        sink.FailNextPushFor(externalStudentId);
        return Results.Ok(new { externalStudentId, armed = true });
    });

    // Outbox seed (Staff) — mirror of Student.
    app.MapPost("/admin/outbox/staff/{externalStaffId}", async (
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
            FirstName = body?.FirstName ?? "PushedFirst",
            LastName = body?.LastName ?? "PushedLast",
            Email = body?.Email ?? $"{externalStaffId.ToLowerInvariant()}@push.test",
            Department = body?.Department ?? "Mathematics",
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

    app.MapGet("/admin/outbox/staff/sink", (InMemoryExternalStaffSink sink) =>
    {
        return Results.Ok(new
        {
            acceptedCount = sink.AcceptedCount,
            accepted = sink.Accepted.Select(kvp => new
            {
                externalStaffId = kvp.Key,
                kvp.Value.FirstName,
                kvp.Value.LastName,
                kvp.Value.Email,
                kvp.Value.Department,
                kvp.Value.ExternalVersion,
                kvp.Value.ExternalUpdatedAt
            })
        });
    });

    app.MapPost("/admin/outbox/staff/sink/fail-next/{externalStaffId}", (
        string externalStaffId,
        InMemoryExternalStaffSink sink) =>
    {
        sink.FailNextPushFor(externalStaffId);
        return Results.Ok(new { externalStaffId, armed = true });
    });

    // Replay — dispatches a fresh run mirroring the original's (Module, Direction).
    // Records the link via a `ReplayOf` tag. Works regardless of original terminal state.
    app.MapPost("/admin/replay/{correlationId:guid}", async (
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
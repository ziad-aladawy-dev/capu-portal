using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CapitalUniversity.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext
{
    private readonly IAppLogger? _logger;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// Modules register their EF configuration assemblies here from their
    /// <c>AddXxxModule()</c> DI extension. <see cref="OnModelCreating"/>
    /// scans each registered assembly for <c>IEntityTypeConfiguration&lt;&gt;</c>
    /// implementations. Static so the registration completes before the first
    /// DbContext is instantiated (modules wire DI at startup, the context is
    /// scoped per-request). No reverse project reference from Core to Module
    /// projects — each module adds itself.
    /// </summary>
    public static List<System.Reflection.Assembly> ModuleConfigurationAssemblies { get; } = new();

    public CoreDbContext(
        DbContextOptions<CoreDbContext> options,
        IAppLogger? logger = null,
        Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null) : base(options)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // The model is assembled from module assemblies registered at runtime
        // (see ModuleConfigurationAssemblies). Key the EF model cache on that
        // assembly set so a context built before a module registers does not
        // pin a model that omits the module's entities.
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, ModuleAwareModelCacheKeyFactory>();
    }

    public DbSet<StructureNode> StructureNodes => Set<StructureNode>();

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Semester> Semesters => Set<Semester>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Staff> Staffs => Set<Staff>();

    public DbSet<Module> Modules { get; set; }
    public DbSet<Resource> Resources { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<StaffRoleAssignment> StaffRoles { get; set; }
    public DbSet<StaffPermissionOverride> StaffPermissions { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<CapitalUniversity.Core.Domain.Outbox.OutboxMessage> OutboxMessages { get; set; }
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<AcademicPlan> AcademicPlans => Set<AcademicPlan>();
    public DbSet<AcademicPlanCourse> AcademicPlanCourses => Set<AcademicPlanCourse>();
    // Invoice / InvoiceItem / PaymentTransaction / StudentProfileRecord
    // DbSets removed — those types now live in their respective module
    // projects (Module.Payments, Module.Student), which Core.Infrastructure
    // must not reference (cycle). Module code uses
    // <c>_context.Set&lt;T&gt;()</c> instead. EF still tracks the entities
    // because each module contributes its assembly to
    // <see cref="ModuleConfigurationAssemblies"/>.

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // M6 — central audit-field stamping for every entity that derives from
        // BaseEntity. Runs BEFORE AuditChangesAsync so the log captures the
        // values that hit the database, not the pre-stamp state.
        //
        // Scope limitation requested for this rollout: Student, Staff, and
        // StructureNode repositories already set UpdatedAt by hand inside
        // every mutator and we are not refactoring those repos in this pass,
        // so we leave their manual stamping in place. The central pass below
        // simply re-writes the same value (last-write-wins, deterministic).
        // When those repositories migrate to the central pattern, drop the
        // manual stamping rather than this guard.
        StampAuditFields();

        if (_logger != null)
        {
            await AuditChangesAsync();
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void StampAuditFields()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Respect explicit CreatedAt from seeders / migrations
                    // (BaseEntity defaults to DateTime.UtcNow at construction).
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = now;
                    }
                    break;
                case EntityState.Modified:
                    // M6 — fill in UpdatedAt even when the caller forgot.
                    // Repositories that already set UpdatedAt manually
                    // (StudentRepository, StaffRepository, StructureNodeRepository)
                    // emit the same value milliseconds earlier — harmless.
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }

    private async Task AuditChangesAsync()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        // Snapshot the request context once so every entry in this SaveChanges
        // carries the same actor (user id / full name / role / IP). Null for
        // background work (e.g. the Sync host), where there is no HttpContext —
        // those entries are correctly recorded as system actions.
        var httpContext = _httpContextAccessor?.HttpContext;

        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;
            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => entry.State.ToString()
            };

            var metadata = new Dictionary<string, object>
            {
                // Reserved keys — lifted onto LogEntry columns by the logger.
                { AuditMetadataKeys.Category, LogCategory.Data },
                { AuditMetadataKeys.Action, action },
                { AuditMetadataKeys.Entity, entityName },
            };

            if (entry.State == EntityState.Modified)
            {
                var changes = entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name, p => new { Old = p.OriginalValue, New = p.CurrentValue });
                metadata.Add("Changes", changes);
            }

            await _logger!.LogInfoAsync($"{action} {entityName}", "AuditTrail", httpContext, metadata);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new StudentConfiguration());
        modelBuilder.ApplyConfiguration(new StaffConfiguration());
        modelBuilder.ApplyConfiguration(new ModuleConfiguration());
        modelBuilder.ApplyConfiguration(new ResourceConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new StaffRoleConfiguration());
        modelBuilder.ApplyConfiguration(new StaffPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new AcademicYearConfiguration());
        modelBuilder.ApplyConfiguration(new SemesterConfiguration());

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CoreDbContext).Assembly);

        // Module-provided EF configurations. Each module (e.g. Payments) calls
        // CoreDbContext.ModuleConfigurationAssemblies.Add(...) from its
        // AddXxxModule() DI extension before the context is first used.
        // Copy the list to avoid "collection modified" errors during model build.
        var modules = ModuleConfigurationAssemblies.ToList();
        foreach (var assembly in modules)
        {
            if (assembly != null)
            {
                modelBuilder.ApplyConfigurationsFromAssembly(assembly);
            }
        }

        modelBuilder.Entity<StructureNode>()
            .HasQueryFilter(x => !x.IsDeleted);

        // P0.6 / P1.5 — soft-delete global query filters for module-owned
        // entities (StudentProfileRecord, Invoice, PaymentTransaction) are
        // declared inside their IEntityTypeConfiguration impls in their
        // respective module projects, since those types live outside this
        // assembly.
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // P3.1 — UTC enforcement. SQL Server datetime2 has no tz info, so we
        // standardise to UTC on the way in and tag DateTimeKind.Utc on the way
        // out. Prevents the well-known "I read what I just wrote, but the Kind
        // is Unspecified" trap that breaks DateTime comparisons.
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<NullableUtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        // Kind=Local genuinely shifts to UTC. Kind=Unspecified is treated as
        // already-UTC (the common case for `new DateTime(2026, 1, 1)` literals
        // and EF in-memory seeded data) — only the Kind is normalised, no
        // wall-clock shift. Reads always come back tagged Utc.
        public UtcDateTimeConverter()
            : base(
                v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        { }
    }

    private sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter()
            : base(
                v => v.HasValue ? NormaliseToUtc(v.Value) : (DateTime?)null,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null)
        { }

        private static DateTime NormaliseToUtc(DateTime value) =>
            value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}

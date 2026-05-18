using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Payments;
using CapitalUniversity.Core.Domain.StudentInformation;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace CapitalUniversity.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext
{
    private readonly IAppLogger? _logger;

    public CoreDbContext(DbContextOptions<CoreDbContext> options, IAppLogger? logger = null) : base(options) 
    {
        _logger = logger;
    }

    public DbSet<StructureNode> StructureNodes => Set<StructureNode>();

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Semester> Semesters => Set<Semester>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Staff> Staffs => Set<Staff>();

    public DbSet<Module> Modules { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<RolePermissionScope> RolePermissionScopes { get; set; }
    public DbSet<StaffRoleAssignment> StaffRoles { get; set; }
    public DbSet<StaffPermissionOverride> StaffPermissions { get; set; }
    public DbSet<StaffPermissionScope> StaffPermissionScopes { get; set; }
    public DbSet<StaffPermissionOverride> StaffPermissionOverrides { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<CapitalUniversity.Core.Domain.Outbox.OutboxMessage> OutboxMessages { get; set; }
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<AcademicPlan> AcademicPlans => Set<AcademicPlan>();
    public DbSet<AcademicPlanCourse> AcademicPlanCourses => Set<AcademicPlanCourse>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<StudentProfileRecord> StudentProfileRecords => Set<StudentProfileRecord>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_logger != null)
        {
            await AuditChangesAsync();
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task AuditChangesAsync()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var metadata = new Dictionary<string, object>
            {
                { "Entity", entry.Entity.GetType().Name },
                { "State", entry.State.ToString() }
            };

            if (entry.State == EntityState.Modified)
            {
                var changes = entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name, p => new { Old = p.OriginalValue, New = p.CurrentValue });
                metadata.Add("Changes", changes);
            }

            await _logger!.LogInfoAsync($"Entity {entry.State}: {entry.Entity.GetType().Name}", "AuditTrail", null, metadata);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new StudentConfiguration());
        modelBuilder.ApplyConfiguration(new StaffConfiguration());
        modelBuilder.ApplyConfiguration(new ModuleConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionScopeConfiguration());
        modelBuilder.ApplyConfiguration(new StaffRoleConfiguration());
        modelBuilder.ApplyConfiguration(new StaffPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new StaffPermissionScopeConfiguration());
        modelBuilder.ApplyConfiguration(new AcademicYearConfiguration());
        modelBuilder.ApplyConfiguration(new SemesterConfiguration());

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CoreDbContext).Assembly);

        modelBuilder.Entity<StructureNode>()
            .HasQueryFilter(x => !x.IsDeleted);

        // P0.6 / P1.5 — soft-delete global query filter for the three listed
        // entities only. Other entities keep BaseEntity.IsDeleted but are not
        // filtered (per the plan's "Apply ONLY to" rule).
        modelBuilder.Entity<Invoice>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<PaymentTransaction>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<StudentProfileRecord>().HasQueryFilter(x => !x.IsDeleted);
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
                v => v.HasValue
                    ? (v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
                    : (DateTime?)null,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null)
        { }
    }
}

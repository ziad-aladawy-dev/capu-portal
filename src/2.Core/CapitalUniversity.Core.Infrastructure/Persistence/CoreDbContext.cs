using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
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
    }
}

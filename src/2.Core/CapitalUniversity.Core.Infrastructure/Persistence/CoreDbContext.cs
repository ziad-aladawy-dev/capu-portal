using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.Users;
using CapitalUniversity.Core.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options) { }

    public DbSet<StructureNode> StructureNodes => Set<StructureNode>();

    public DbSet<Student> Students { get; set; }
    public DbSet<Staff> Staffs { get; set; }
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        
    }
}
using CapitalUniversity.Core.Domain.Academic;
using CapitalUniversity.Core.Domain.AcademicCalendar;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options) { }

    public DbSet<University> Universities { get; set; }
    public DbSet<Faculty> Faculties { get; set; }
    public DbSet<FacultySystem> FacultySystems { get; set; }
    public DbSet<AcademicProgram> AcademicPrograms { get; set; } 
    public DbSet<Level> Levels { get; set; }
    public DbSet<Course> Courses { get; set; } 

    public DbSet<AcademicYear> AcademicYears { get; set; }
    public DbSet<Semester> Semesters { get; set; }

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UniversityConfiguration());
        modelBuilder.ApplyConfiguration(new FacultyConfiguration());
        modelBuilder.ApplyConfiguration(new FacultySystemConfiguration());
        modelBuilder.ApplyConfiguration(new AcademicProgramConfiguration());
        modelBuilder.ApplyConfiguration(new LevelConfiguration());
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new AcademicYearConfiguration());
        modelBuilder.ApplyConfiguration(new SemesterConfiguration());
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
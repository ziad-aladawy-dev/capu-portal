using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options) { }

    public DbSet<StructureNode> StructureNodes => Set<StructureNode>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Staff> Staff => Set<Staff>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new StructureNodeConfiguration());

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CoreDbContext).Assembly);

        modelBuilder.Entity<StructureNode>()
            .HasQueryFilter(x => !x.IsDeleted);
    }
}
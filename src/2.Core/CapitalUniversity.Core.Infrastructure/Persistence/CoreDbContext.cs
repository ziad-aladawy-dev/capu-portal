using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options) { }

    public DbSet<StructureNode> StructureNodes => Set<StructureNode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new StructureNodeConfiguration());

        modelBuilder.Entity<StructureNode>()
            .HasQueryFilter(x => !x.IsDeleted);
    }
}
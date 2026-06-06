using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CapitalUniversity.Sync.Persistence.Context;

/// <summary>
/// Used only by the `dotnet ef` tooling to construct a <see cref="SyncDbContext"/>
/// at design time. The connection string here is dev-only.
/// </summary>
public sealed class SyncDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SyncDbContext>
{
    public SyncDbContext CreateDbContext(string[] args)
    {
        const string devConnectionString =
            "Server=localhost,1433;Database=CapitalUniversityDb;User Id=SA;Password=CUP_Str0ng!Pass;TrustServerCertificate=True;Encrypt=False;";

        var options = new DbContextOptionsBuilder<SyncDbContext>()
            .UseSqlServer(
                devConnectionString,
                sql => sql.MigrationsHistoryTable("__SyncMigrationsHistory", SyncDbContext.SchemaName))
            .Options;

        return new SyncDbContext(options);
    }
}
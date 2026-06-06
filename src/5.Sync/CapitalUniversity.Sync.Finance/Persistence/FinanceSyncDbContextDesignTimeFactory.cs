using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CapitalUniversity.Sync.Finance.Persistence;

public sealed class FinanceSyncDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FinanceSyncDbContext>
{
    public FinanceSyncDbContext CreateDbContext(string[] args)
    {
        const string devConnectionString =
            "Server=localhost,1433;Database=CapitalUniversityDb;User Id=SA;Password=CUP_Str0ng!Pass;TrustServerCertificate=True;Encrypt=False;";

        var options = new DbContextOptionsBuilder<FinanceSyncDbContext>()
            .UseSqlServer(
                devConnectionString,
                sql => sql.MigrationsHistoryTable("__FinanceSyncMigrationsHistory", FinanceSyncDbContext.SchemaName))
            .Options;

        return new FinanceSyncDbContext(options);
    }
}

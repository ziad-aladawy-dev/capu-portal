using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CapitalUniversity.Sync.Staff.Persistence;

public sealed class StaffSyncDbContextDesignTimeFactory : IDesignTimeDbContextFactory<StaffSyncDbContext>
{
    public StaffSyncDbContext CreateDbContext(string[] args)
    {
        const string devConnectionString =
            "Server=localhost,1433;Database=CapitalUniversityDb;User Id=SA;Password=CUP_Str0ng!Pass;TrustServerCertificate=True;Encrypt=False;";

        var options = new DbContextOptionsBuilder<StaffSyncDbContext>()
            .UseSqlServer(
                devConnectionString,
                sql => sql.MigrationsHistoryTable("__StaffSyncMigrationsHistory", StaffSyncDbContext.SchemaName))
            .Options;

        return new StaffSyncDbContext(options);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CapitalUniversity.Sync.Schedules.Persistence;

public sealed class SchedulesSyncDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SchedulesSyncDbContext>
{
    public SchedulesSyncDbContext CreateDbContext(string[] args)
    {
        const string devConnectionString =
            "Server=localhost,1433;Database=CapitalUniversityDb;User Id=SA;Password=CUP_Str0ng!Pass;TrustServerCertificate=True;Encrypt=False;";

        var options = new DbContextOptionsBuilder<SchedulesSyncDbContext>()
            .UseSqlServer(
                devConnectionString,
                sql => sql.MigrationsHistoryTable("__SchedulesSyncMigrationsHistory", SchedulesSyncDbContext.SchemaName))
            .Options;

        return new SchedulesSyncDbContext(options);
    }
}

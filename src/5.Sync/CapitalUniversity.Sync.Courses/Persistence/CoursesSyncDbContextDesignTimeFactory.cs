using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CapitalUniversity.Sync.Courses.Persistence;

public sealed class CoursesSyncDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CoursesSyncDbContext>
{
    public CoursesSyncDbContext CreateDbContext(string[] args)
    {
        const string devConnectionString =
            "Server=localhost,1433;Database=CapitalUniversityDb;User Id=SA;Password=CUP_Str0ng!Pass;TrustServerCertificate=True;Encrypt=False;";

        var options = new DbContextOptionsBuilder<CoursesSyncDbContext>()
            .UseSqlServer(
                devConnectionString,
                sql => sql.MigrationsHistoryTable("__CoursesSyncMigrationsHistory", CoursesSyncDbContext.SchemaName))
            .Options;

        return new CoursesSyncDbContext(options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CapitalUniversity.Sync.Student.Persistence;

public sealed class StudentSyncDbContextDesignTimeFactory : IDesignTimeDbContextFactory<StudentSyncDbContext>
{
    public StudentSyncDbContext CreateDbContext(string[] args)
    {
        const string devConnectionString =
            "Server=localhost,1433;Database=CapitalUniversityDb;User Id=SA;Password=CUP_Str0ng!Pass;TrustServerCertificate=True;Encrypt=False;";

        var options = new DbContextOptionsBuilder<StudentSyncDbContext>()
            .UseSqlServer(
                devConnectionString,
                sql => sql.MigrationsHistoryTable("__StudentSyncMigrationsHistory", StudentSyncDbContext.SchemaName))
            .Options;

        return new StudentSyncDbContext(options);
    }
}
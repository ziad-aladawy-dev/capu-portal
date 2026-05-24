using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace CapitalUniversity.Contract.Tests._Infra;

/// <summary>
/// Variant of <see cref="WebApplicationFactory{TEntryPoint}"/> that swaps
/// the API's default <see cref="CoreDbContext"/> registration for one
/// pointed at a per-instance SQL Server database inside the shared
/// Testcontainers MSSQL container. Every other piece of the production
/// composition (controllers, middleware, auth, JWT, services, seeders)
/// runs exactly as it does in <c>dotnet run</c>.
///
/// <para>
/// <b>Why a custom factory.</b> The existing
/// <see cref="Phase1IntegrationTests"/> uses an InMemory DbContext, which
/// is good enough for HTTP-shape assertions but cannot represent the
/// realistic latency / throughput of 3 – 5 k-row bulk operations.
/// Hitting <c>/api/students/export/csv</c> against InMemory would report
/// numbers an order of magnitude faster than production. This factory is
/// the SQL-backed sibling — measurements taken through it reflect what
/// real users will experience.
/// </para>
///
/// <para>
/// <b>Database lifecycle.</b> Implements <see cref="IAsyncLifetime"/> so
/// xUnit calls <see cref="InitializeAsync"/> before the test class is
/// used and <see cref="DisposeAsync"/> on teardown. The factory creates a
/// uniquely-named database in the shared container, lets the production
/// startup pipeline (DataSeeder, UniversityStructureSeeder, IdentitySeeder,
/// PermissionManifestSynchronizer) run against it, then exposes
/// <see cref="HttpClient"/>s for the tests. On dispose the database is
/// dropped and the SqlClient pool is flushed.
/// </para>
///
/// <para>
/// <b>Skip semantics.</b> If Docker / the env-var override is unreachable
/// at <see cref="InitializeAsync"/>, <see cref="IsAvailable"/> stays false
/// and tests should open with
/// <c>Skip.IfNot(_factory.IsAvailable, _factory.UnavailableReason)</c>.
/// No red CI rows for an environment problem.
/// </para>
/// </summary>
public sealed class SqlServerWebApplicationFactory : WebApplicationFactory<ModulesRegistry>, IAsyncLifetime
{
    private readonly string _databaseName = "CapuPerf_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; private set; } = string.Empty;
    public bool IsAvailable { get; private set; }
    public string UnavailableReason { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var pool = await SqlServerContainerPool.GetAsync().ConfigureAwait(false);
        if (pool.BaseConnectionString is null)
        {
            UnavailableReason = pool.FailReason ?? "Unknown SQL backend failure.";
            return;
        }

        if (pool.BaseConnectionString.Contains("{db}", StringComparison.Ordinal))
        {
            ConnectionString = pool.BaseConnectionString.Replace("{db}", _databaseName);
        }
        else
        {
            var b = new SqlConnectionStringBuilder(pool.BaseConnectionString) { InitialCatalog = _databaseName };
            ConnectionString = b.ConnectionString;
        }

        // Pre-create the schema BEFORE Program.cs's startup pipeline gets
        // to it. The production startup calls `db.Database.MigrateAsync()`
        // which replays the EF migration history; one of those migrations
        // (`20260518154708_r2`) attempts to add `Students.SessionVersion`
        // that an earlier migration already added — fine for the
        // pre-existing dev DB (idempotent in practice when columns exist)
        // but a new SQL test DB fails with "Column name 'SessionVersion'
        // in table 'Students' is specified more than once". We work
        // around it by:
        //   (a) building the schema directly from the EF model (skips
        //       migration history entirely),
        //   (b) overriding `Database:AutoMigrate` to false so Program.cs
        //       skips MigrateAsync — the schema is already present.
        try
        {
            var bootstrapOptions = new DbContextOptionsBuilder<CoreDbContext>()
                .UseSqlServer(ConnectionString).Options;
            await using (var bootstrap = new CoreDbContext(bootstrapOptions))
            {
                await bootstrap.Database.EnsureCreatedAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            UnavailableReason = $"Failed to bootstrap schema on the SQL test database: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}";
            return;
        }

        // CreateClient triggers Build + Program.cs's startup pipeline,
        // which still runs DataSeeder / UniversityStructureSeeder /
        // IdentitySeeder / PermissionManifestSynchronizer against our
        // freshly schema'd database.
        try
        {
            _ = CreateClient();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            UnavailableReason = $"API failed to start against the SQL test database: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}";
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // See InitializeAsync — the test schema is created via the EF
        // model, NOT via migration history. Disable the production
        // startup's MigrateAsync call so it doesn't conflict.
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<CoreDbContext>>();
            services.RemoveAll<CoreDbContext>();
            services.AddDbContext<CoreDbContext>(o => o
                .UseSqlServer(ConnectionString)
                .EnableSensitiveDataLogging(false));

            // Mong / logger seams that the API expects from Program.cs but
            // we do not exercise. Mirror the existing Phase1IntegrationTests
            // pattern — keeps the test rig minimal.
            services.AddScoped(_ => new Mock<IAppLogger>().Object);
            services.AddScoped(_ => new Mock<ILoggerService>().Object);
            services.AddSingleton(_ => new Mock<IMongoClient>().Object);
        });
    }

    public new async Task DisposeAsync()
    {
        try
        {
            var options = new DbContextOptionsBuilder<CoreDbContext>()
                .UseSqlServer(ConnectionString).Options;
            await using var db = new CoreDbContext(options);
            await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
        }
        catch
        {
            // Container will be reaped on process exit; leaked test DB is harmless.
        }
        SqlConnection.ClearAllPools();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}

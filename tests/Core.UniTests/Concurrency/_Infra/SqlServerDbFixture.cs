using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Concurrency._Infra;

/// <summary>
/// Per-test SQL Server database lifecycle for the concurrency suite. Backed
/// by a Docker-hosted SQL Server (Testcontainers.MsSql), with an optional
/// override to point at an external SQL instance via the
/// <c>CAPU_TEST_SQL_CONNECTION</c> env var.
///
/// <para>
/// <b>Public API (unchanged from the LocalDB era).</b>
/// <list type="bullet">
///   <item><see cref="ConnectionString"/> — connection string for THIS
///   test's isolated database.</item>
///   <item><see cref="NewContext"/> — factory for a brand-new
///   <see cref="CoreDbContext"/>; every parallel task in a test calls
///   this to get its own thread-isolated context (DbContext is not
///   thread-safe).</item>
///   <item><see cref="IsAvailable"/> + <see cref="UnavailableReason"/> —
///   tests open with <c>Skip.IfNot(_fixture.IsAvailable, _fixture.UnavailableReason)</c>
///   so a missing Docker daemon or unreachable SQL host degrades cleanly
///   instead of producing red CI rows for an environment problem.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Isolation strategy.</b> xUnit constructs a fresh test-class instance
/// per <c>[Fact]</c> / <c>[SkippableFact]</c>, and <see cref="IAsyncLifetime"/>
/// runs <see cref="InitializeAsync"/> before every test method. The fixture
/// creates a brand-new database (name <c>CapuTest_&lt;guid&gt;</c>) inside
/// the shared SQL Server, builds the schema from the EF model via
/// <see cref="DatabaseFacade.EnsureCreatedAsync"/>, and drops the
/// database on <see cref="DisposeAsync"/>. No two tests ever see each
/// other's rows, and no transaction wrapping is used (a wrapping
/// transaction would hold row-locks and defeat the very concurrency we
/// are validating).
/// </para>
///
/// <para>
/// <b>Container lifecycle.</b> Only ONE SQL Server container is started
/// per test process — see <see cref="SqlServerContainerPool"/>. The
/// container is shared across every fixture and reaped automatically on
/// process exit by Testcontainers' Ryuk sidecar. Per-test isolation lives
/// at the database level inside the container, not at the container
/// level — starting fresh containers per test would add ~15 s × N
/// overhead for no isolation benefit.
/// </para>
///
/// <para>
/// <b>Connection pooling.</b> <see cref="DisposeAsync"/> calls
/// <c>SqlConnection.ClearAllPools()</c> after dropping the database, so
/// pooled connections to the dropped name cannot survive into the next
/// test and trip "Database in use" on the next fixture instance.
/// </para>
/// </summary>
public sealed class SqlServerDbFixture : IAsyncLifetime
{
    public string DatabaseName { get; } = "CapuTest_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// True after <see cref="InitializeAsync"/> succeeds — i.e. Docker is
    /// running OR the <c>CAPU_TEST_SQL_CONNECTION</c> env var points at a
    /// reachable SQL Server, AND the per-test database was created.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// One-line reason the SQL backend was not reachable — surfaced as the
    /// xUnit skip message so the dev sees actionable detail (Docker not
    /// running, env-var malformed, network error, etc.).
    /// </summary>
    public string UnavailableReason { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var pool = await SqlServerContainerPool.GetAsync().ConfigureAwait(false);
        if (pool.BaseConnectionString is null)
        {
            UnavailableReason = pool.FailReason ?? "Unknown SQL backend failure.";
            return;
        }

        // The pool returns one of two shapes:
        // (a) the env-var template (still contains literal "{db}") — Path 1
        //     in SqlServerContainerPool.GetAsync.
        // (b) the container's connection string (Database=master, etc.) —
        //     Path 2 (Docker).
        if (pool.BaseConnectionString.Contains("{db}", StringComparison.Ordinal))
        {
            ConnectionString = pool.BaseConnectionString.Replace("{db}", DatabaseName);
        }
        else
        {
            var builder = new SqlConnectionStringBuilder(pool.BaseConnectionString)
            {
                InitialCatalog = DatabaseName,
            };
            ConnectionString = builder.ConnectionString;
        }

        try
        {
            await using var db = NewContext();
            await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
            IsAvailable = true;
        }
        catch (SqlException ex)
        {
            UnavailableReason =
                $"Connected to SQL Server but failed to create per-test database '{DatabaseName}'. Underlying: {ex.Message.Split('\n')[0]}";
        }
        catch (Exception ex)
        {
            UnavailableReason =
                $"Unexpected error initialising the SQL test database: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}";
        }
    }

    /// <summary>
    /// Brand-new <see cref="CoreDbContext"/> wired to this test's isolated
    /// database. Caller owns disposal. Every parallel task MUST resolve its
    /// own context — sharing across threads is unsafe.
    /// </summary>
    public CoreDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseSqlServer(ConnectionString)
            // Concurrency tests deliberately observe
            // DbUpdateConcurrencyException + unique-violation. Disable
            // EF retry-on-failure here so we see the first failure
            // instead of an opaque retry-wrap.
            .Options;
        return new CoreDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable) return;
        try
        {
            await using var db = NewContext();
            await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort cleanup. The container will be reaped on
            // process exit by Ryuk in any case, so a leaked test database
            // inside it is harmless.
        }
        SqlConnection.ClearAllPools();
    }
}

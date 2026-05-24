using Xunit;

namespace CapitalUniversity.Core.UniTests.Concurrency._Infra;

/// <summary>
/// Force xUnit to serialise every test class that talks to the shared
/// Testcontainers SQL Server instance. xUnit's default behaviour is to
/// run different test classes in parallel within the same assembly; that
/// works fine for in-memory tests but spawns N concurrent
/// <c>EnsureCreatedAsync</c> / <c>EnsureDeletedAsync</c> calls against the
/// container, which can saturate it (the test host crashes with no
/// readable error when the container engine starts refusing).
///
/// <para>
/// Tests in this collection still run sequentially relative to each
/// other AND each test still gets its OWN per-test database via
/// <see cref="SqlServerDbFixture"/> — the collection only widens the
/// "no parallel classes" guarantee that xUnit already provides
/// inside a single class.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerTestCollection
{
    public const string Name = "SqlServer (Testcontainers)";
}

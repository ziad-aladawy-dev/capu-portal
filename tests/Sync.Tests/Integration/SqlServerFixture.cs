using Testcontainers.MsSql;

namespace CapitalUniversity.Sync.Tests.Integration;

/// <summary>
/// Shared SQL Server Testcontainer for the integration suite. Spins one container
/// per xUnit collection and tears it down after the last test. Containers need
/// Docker on the host — local devs running without Docker will see the integration
/// tests skipped via the <see cref="SkipOnNoDockerFactAttribute"/> guard.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception ex) when (IsDockerUnavailable(ex))
        {
            // Mark the fixture as unavailable; tests using SkipOnNoDockerFact will
            // self-skip. Don't let the whole assembly fail to load.
            DockerUnavailable = true;
            DockerError = ex.Message;
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public bool DockerUnavailable { get; private set; }
    public string? DockerError { get; private set; }

    private static bool IsDockerUnavailable(Exception ex)
    {
        // Testcontainers surfaces Docker-not-installed / daemon-down / ECONNREFUSED
        // through a handful of shapes; treat any of them as "skip the suite".
        var msg = (ex.Message ?? "") + " " + (ex.InnerException?.Message ?? "");
        return msg.Contains("Docker", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("daemon", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("pipe", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("connection refused", StringComparison.OrdinalIgnoreCase);
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
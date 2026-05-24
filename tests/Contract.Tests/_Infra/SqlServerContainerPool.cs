using Testcontainers.MsSql;

namespace CapitalUniversity.Contract.Tests._Infra;

/// <summary>
/// Process-singleton SQL Server container shared by every test in this
/// project. Mirrors the same-named helper in Core.UniTests — duplicated
/// rather than referenced so xUnit isolates each test assembly's process
/// and we don't need a cross-test-project assembly reference.
///
/// <para>
/// First caller to <see cref="GetAsync"/> wins the lock and starts the
/// container. Subsequent callers (whether from parallel test fixtures or
/// sequential ones) get the same already-started instance. On process
/// exit, Testcontainers' Ryuk sidecar reaps the container even if no
/// explicit dispose runs.
/// </para>
///
/// <para>
/// <c>CAPU_TEST_SQL_CONNECTION</c> env-var override (template containing
/// literal <c>{db}</c>) bypasses Docker and points at a pre-existing SQL
/// instance — used in CI environments that already ship SQL Server.
/// </para>
/// </summary>
internal static class SqlServerContainerPool
{
    public const string EnvVarConnectionTemplate = "CAPU_TEST_SQL_CONNECTION";

    private const string SaPassword = "Capu_Test!_StrongP@ssw0rd_2026";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static MsSqlContainer? _container;
    private static bool _probed;
    private static string? _failReason;

    public readonly record struct PoolResult(string? BaseConnectionString, string? FailReason);

    public static async Task<PoolResult> GetAsync()
    {
        if (_probed)
        {
            return new PoolResult(_container?.GetConnectionString(), _failReason);
        }

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_probed)
            {
                return new PoolResult(_container?.GetConnectionString(), _failReason);
            }

            var envTemplate = Environment.GetEnvironmentVariable(EnvVarConnectionTemplate);
            if (!string.IsNullOrWhiteSpace(envTemplate))
            {
                if (!envTemplate.Contains("{db}", StringComparison.Ordinal))
                {
                    _failReason =
                        $"'{EnvVarConnectionTemplate}' must contain the literal '{{db}}' placeholder so the fixture can substitute a per-test database name.";
                    _probed = true;
                    return new PoolResult(null, _failReason);
                }
                _probed = true;
                return new PoolResult(envTemplate, null);
            }

            try
            {
                var built = new MsSqlBuilder()
                    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                    .WithPassword(SaPassword)
                    .Build();

                await built.StartAsync().ConfigureAwait(false);
                _container = built;
            }
            catch (Exception ex)
            {
                _failReason =
                    $"Docker-based SQL Server unavailable. Either run Docker / Docker Desktop, or set the {EnvVarConnectionTemplate} env var to a connection-string template (must contain literal '{{db}}'). Underlying: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}";
            }
            _probed = true;
            return new PoolResult(_container?.GetConnectionString(), _failReason);
        }
        finally
        {
            Gate.Release();
        }
    }
}

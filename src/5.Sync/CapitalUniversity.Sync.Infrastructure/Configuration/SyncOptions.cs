using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Infrastructure.Configuration;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    public SyncHangfireOptions Hangfire { get; set; } = new();

    /// <summary>
    /// Pipeline-wide tuning knobs (Phase 8). Per-batch writer-retry, throughput
    /// metric, etc. Defaults preserve Phase 7 behavior.
    /// </summary>
    public SyncPipelineOptions Pipeline { get; set; } = new();

    /// <summary>
    /// Routes a sync run to a Hangfire queue. Lookup order:
    ///   1. "{moduleName}|{direction}" (e.g. "students|Push")
    ///   2. "{moduleName}"             (e.g. "students")
    ///   3. <see cref="DefaultQueue"/>
    ///
    /// <para>
    /// The direction separator is <c>|</c> (pipe) rather than <c>:</c> because
    /// <c>:</c> is the .NET configuration path separator and would cause JSON
    /// keys like <c>"students:Push"</c> to collide with the existing
    /// <c>"students"</c> string value during binding.
    /// </para>
    /// </summary>
    public Dictionary<string, string> ModuleQueues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string DefaultQueue { get; set; } = "default";

    /// <summary>
    /// Defence-in-depth gate for the Hangfire dashboard and the verification/operator
    /// admin endpoints. Default <c>false</c> — even Development must opt in explicitly.
    /// Production NEVER flips this true without first wiring real auth (the dashboard
    /// has no internal auth and the admin endpoints accept anonymous POSTs).
    ///
    /// <para>
    /// The host requires BOTH <c>IsDevelopment()</c> AND this flag. Two independent
    /// signals — relying on a single env var alone proved too easy to misconfigure
    /// (e.g. base Docker images shipping with <c>ASPNETCORE_ENVIRONMENT=Development</c>).
    /// </para>
    /// </summary>
    public bool ExposeAdminEndpoints { get; set; } = false;

    public string ResolveQueue(string moduleName, SyncDirection direction)
    {
        if (ModuleQueues.TryGetValue($"{moduleName}|{direction}", out var directionScoped))
        {
            return directionScoped;
        }

        if (ModuleQueues.TryGetValue(moduleName, out var moduleScoped))
        {
            return moduleScoped;
        }

        return DefaultQueue;
    }
}
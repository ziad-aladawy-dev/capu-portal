namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Phase X.2 fix #3 — HTTP-adapter wiring. Operators set
/// <c>Sync:Integration:UseHttpAdapters = true</c> to replace the in-memory
/// per-module sources/sinks with HTTP-client implementations. Defaults to
/// <c>false</c> so dev/test behavior is unchanged — opt-in only.
///
/// <para>
/// The HTTP adapters live in <c>Sync.Host</c> (not in module projects) and are
/// registered AFTER <c>AddStudentSync</c>/<c>AddStaffSync</c> so DI's
/// last-registration-wins semantics make them the active <see cref="IExternalStudentSink"/>
/// (etc.) without touching the module code. The InMemory implementations stay
/// registered as concrete classes for diagnostics endpoints (the
/// <c>/admin/outbox/sink</c> inspect surface still resolves <c>InMemoryExternalStudentSink</c>
/// directly, so operators can see what would have been sent in a parallel test
/// configuration with the flag off).
/// </para>
/// </summary>
public sealed class SyncIntegrationOptions
{
    public const string SectionName = "Sync:Integration";

    /// <summary>Master switch. <c>false</c> keeps the in-memory sources/sinks active.</summary>
    public bool UseHttpAdapters { get; set; } = false;

    public StudentHttpAdapterOptions Student { get; set; } = new();
    public StaffHttpAdapterOptions Staff { get; set; } = new();
}

public sealed class StudentHttpAdapterOptions
{
    /// <summary>Base URL of the external Student system. Required when
    /// <see cref="SyncIntegrationOptions.UseHttpAdapters"/> is true.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Optional API key — sent as <c>X-Api-Key</c>. Move to user-secrets
    /// in production.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Per-request timeout. Default 30s.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class StaffHttpAdapterOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
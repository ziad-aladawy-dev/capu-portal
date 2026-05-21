using System.Globalization;

namespace CapitalUniversity.Core.Application.CrossCutting.Localization;

/// <summary>
/// Lightweight <see cref="IDisposable"/> scope that swaps the ambient
/// <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/>
/// for the lifetime of a <c>using</c> block, then restores the previous
/// values on dispose.
///
/// <para>
/// Intended for background tasks (e.g. hosted services, queue consumers,
/// schedulers) that have no <c>HttpContext</c> and therefore no
/// <c>Accept-Language</c> header to drive <c>CurrentCultureService</c>.
/// Wrap a unit of work in this scope to make any culture-aware formatting
/// (logs, exception messages, date / number rendering) emit under a known
/// culture instead of inheriting whatever the thread happened to be on.
/// </para>
///
/// <para>
/// <b>Async behavior:</b> <see cref="CultureInfo.CurrentCulture"/> and
/// <see cref="CultureInfo.CurrentUICulture"/> flow with the
/// <c>ExecutionContext</c> in modern .NET, so an awaited continuation
/// inside the scope still sees the scoped culture. Dispose must run on the
/// same logical flow that opened the scope (the standard <c>using</c>
/// pattern guarantees this).
/// </para>
///
/// <para>
/// <b>What this is not:</b> this is not a global culture mutation, not a
/// DI-registered service, and not a replacement for
/// <see cref="Abstractions.CrossCutting.Localization.ICurrentCultureService"/>.
/// It is a per-call-stack helper for non-HTTP entry points. It does not
/// alter <c>CultureInfo.DefaultThreadCurrentCulture</c>; the process-wide
/// default remains untouched.
/// </para>
///
/// <example>
/// <code>
/// // In a hosted background service:
/// using (SystemCultureScope.English())
/// {
///     await DoWorkAsync(ct); // any throw / log inside renders in "en"
/// }
/// // previous culture restored here
/// </code>
/// </example>
/// </summary>
public sealed class SystemCultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;
    private bool _disposed;

    /// <summary>
    /// Open a scope that sets both <see cref="CultureInfo.CurrentCulture"/>
    /// and <see cref="CultureInfo.CurrentUICulture"/> to the same culture.
    /// </summary>
    /// <param name="culture">The culture to apply. Must not be <c>null</c>.</param>
    public SystemCultureScope(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Open a scope from a culture name (e.g. <c>"en"</c>, <c>"ar"</c>).
    /// Uses <see cref="CultureInfo.GetCultureInfo(string)"/> so repeated
    /// calls share the same cached instance.
    /// </summary>
    /// <exception cref="CultureNotFoundException">If the name is unknown to the runtime.</exception>
    public SystemCultureScope(string cultureName)
        : this(CultureInfo.GetCultureInfo(cultureName ?? throw new ArgumentNullException(nameof(cultureName))))
    {
    }

    /// <summary>Convenience factory for an English (<c>"en"</c>) scope.</summary>
    public static SystemCultureScope English() => new("en");

    /// <summary>Convenience factory for an Arabic (<c>"ar"</c>) scope — the project's default culture.</summary>
    public static SystemCultureScope Arabic() => new("ar");

    public void Dispose()
    {
        // Idempotent: re-disposing is a no-op so a double-using or a
        // dispose-in-finally on an already-disposed scope is safe.
        if (_disposed) return;
        _disposed = true;
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }
}

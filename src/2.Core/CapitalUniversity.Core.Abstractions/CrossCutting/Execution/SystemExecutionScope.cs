using System;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Execution;

/// <summary>
/// Lightweight scope that elevates the current logical flow to "Trusted System"
/// status. While the scope is open, <see cref="IExecutionContext.IsSystem"/>
/// is true, allowing authorization guards (like <c>EffectiveScope</c>) to
/// permit operations even when no <c>HttpContext</c> or authenticated user
/// is present.
///
/// <para>
/// <b>Usage:</b> wrap background / outbox processing blocks. This is NOT
/// for use in controllers or user-driven flows.
/// </para>
/// </summary>
public sealed class SystemExecutionScope : IDisposable
{
    private readonly IExecutionContext _context;
    private readonly bool _previousMode;
    private bool _disposed;

    public SystemExecutionScope(IExecutionContext context)
    {
        _context = context;
        _previousMode = context.IsSystem;
        context.SetSystemMode(true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _context.SetSystemMode(_previousMode);
    }
}

using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

namespace CapitalUniversity.API.Infrastructure;

/// <summary>
/// M14 — Eagerly resolves the request-scoped <see cref="IUserScope"/> snapshot
/// right after authentication so the rest of the pipeline never hits the
/// sync-over-async path in <c>UserScope.EnsureLoaded()</c>. Without this
/// preload, every read of <see cref="IUserScope.UserId"/> /
/// <see cref="IUserScope.OwnStructureNodePath"/> / etc. from synchronous
/// controller or repository code blocks on
/// <c>LoadAsync().GetAwaiter().GetResult()</c>, which is a known thread-pool
/// starvation risk under load.
///
/// <para>
/// Anonymous requests skip — <see cref="IUserScope.EnsureLoadedAsync"/>
/// short-circuits when <see cref="ICurrentUser.IsAuthenticated"/> is false,
/// so there's no work to do.
/// </para>
/// </summary>
public class UserScopePreloadMiddleware
{
    private readonly RequestDelegate _next;

    public UserScopePreloadMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserScope userScope)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            await userScope.EnsureLoadedAsync(context.RequestAborted);
        }

        await _next(context);
    }
}

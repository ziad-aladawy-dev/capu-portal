using CapitalUniversity.Core.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Contract.Tests;

/// <summary>
/// Test convenience: expand a CRUD "top action" into the per-action
/// <see cref="RolePermission"/> rows the production schema stores. Mirrors the
/// canonical CRUD ladder (View &lt; Insert &lt; EditClose &lt; Open &lt; Delete):
/// granting a verb writes one row per action up to and including it.
/// </summary>
internal static class TestPermissionGrantHelper
{
    private static readonly string[] CrudLadder = { "View", "Insert", "EditClose", "Open", "Delete" };

    public static IEnumerable<string> ExpandCrud(string topAction)
    {
        var idx = Array.IndexOf(CrudLadder, topAction);
        if (idx < 0) yield break;
        for (var i = 0; i <= idx; i++) yield return CrudLadder[i];
    }

    public static IEnumerable<RolePermission> GrantsFor(Guid roleId, Guid resourceId, string topAction) =>
        ExpandCrud(topAction).Select(action => new RolePermission(roleId, resourceId, action));

    public static void AddCrudGrant<TDbContext>(this TDbContext context, Guid roleId, Guid resourceId, string topAction)
        where TDbContext : DbContext
    {
        var set = context.Set<RolePermission>();
        foreach (var rp in GrantsFor(roleId, resourceId, topAction))
        {
            set.Add(rp);
        }
    }
}

using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Contract.Tests;

/// <summary>
/// Test convenience: translate the legacy <see cref="ActionLevel"/> ladder a
/// test wants to express into the per-action <see cref="RolePermission"/> rows
/// the production schema now stores. Mirrors the seeder's
/// <c>ExpandLegacyCrudLevel</c>: granting <c>Level=N</c> writes one row per
/// action up to and including <c>N</c> on the canonical CRUD ladder.
/// </summary>
internal static class TestPermissionGrantHelper
{
    public static IEnumerable<string> ExpandCrud(ActionLevel level)
    {
        if (level >= ActionLevel.View)      yield return "View";
        if (level >= ActionLevel.Insert)    yield return "Insert";
        if (level >= ActionLevel.EditClose) yield return "EditClose";
        if (level >= ActionLevel.Open)      yield return "Open";
        if (level >= ActionLevel.Delete)    yield return "Delete";
    }

    public static IEnumerable<RolePermission> GrantsFor(Guid roleId, Guid resourceId, ActionLevel level) =>
        ExpandCrud(level).Select(action => new RolePermission(roleId, resourceId, action));

    public static void AddCrudGrant<TDbContext>(this TDbContext context, Guid roleId, Guid resourceId, ActionLevel level)
        where TDbContext : DbContext
    {
        var set = context.Set<RolePermission>();
        foreach (var rp in GrantsFor(roleId, resourceId, level))
        {
            set.Add(rp);
        }
    }
}
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Host.Notifications;

/// <summary>
/// <see cref="ISyncOutcomeNotifier"/> implementation that persists an in-app
/// notification into Core for every user who can access the sync layer.
///
/// <para>
/// "Who can access sync" is resolved from the permission graph, not a hard-coded
/// role: the <c>SyncPermissionManifest</c> declares Module=<c>sync</c>,
/// Resource=<c>sync</c>. A staff member is a recipient when they hold ANY
/// effective action on that resource, where effective mirrors the runtime
/// evaluator: <c>(role grants ∪ Allow overrides) − Deny overrides</c>, matched
/// per (scope, action), with expired overrides ignored. So a per-staff Allow
/// override grants access without a role, and a Deny override at the same
/// scope+action revokes a role grant.
/// </para>
///
/// <para>
/// This is the ONLY sync-side type that writes Core notification rows, so it lives
/// in the Host (the single sync project that references Core.Infrastructure) and
/// writes through <see cref="CoreDbContext"/> directly. It does not share a
/// transaction with the sync audit DB (a different context), so delivery is
/// best-effort and decoupled from the run's audit write — by design.
/// </para>
/// </summary>
public sealed class CoreSyncOutcomeNotifier : ISyncOutcomeNotifier
{
    // Matches SyncPermissionManifest (Module="sync", Resource="sync").
    private const string SyncModuleKey = "sync";
    private const string SyncResourceKey = "sync";
    private const int MaxErrorChars = 200;

    private readonly CoreDbContext _db;
    private readonly ILogger<CoreSyncOutcomeNotifier> _logger;

    public CoreSyncOutcomeNotifier(CoreDbContext db, ILogger<CoreSyncOutcomeNotifier> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task NotifyAsync(SyncOutcomeNotice notice, CancellationToken cancellationToken)
    {
        var recipientIds = await ResolveRecipientIdsAsync(cancellationToken);

        if (recipientIds.Count == 0)
        {
            _logger.LogInformation(
                "Sync outcome notification skipped — no active staff hold the '{Resource}' permission. Module={Module} Success={Success} CorrelationId={CorrelationId}",
                SyncResourceKey, notice.ModuleName, notice.Success, notice.CorrelationId);
            return;
        }

        var (title, message, type) = BuildContent(notice);
        var now = DateTime.UtcNow;

        foreach (var recipientId in recipientIds)
        {
            _db.Notifications.Add(new Notification
            {
                RecipientUserId = recipientId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Sync outcome notification delivered to {Count} sync-permission holder(s). Module={Module} Direction={Direction} Success={Success} CorrelationId={CorrelationId}",
            recipientIds.Count, notice.ModuleName, notice.Direction, notice.Success, notice.CorrelationId);
    }

    // One row per (staff, scope, action) grant. Value-type equality lets a Deny
    // override cancel exactly the matching Allow (same staff + scope + action),
    // mirroring the runtime evaluator's per-scope/per-action set algebra.
    private readonly record struct GrantKey(
        Guid StaffId,
        Guid? StructureNodeId,
        string? StructureNodePath,
        string Year,
        string Semester,
        string Action);

    /// <summary>
    /// Active staff who hold ANY effective action on the <c>sync</c> resource —
    /// <c>(role grants ∪ Allow overrides) − Deny overrides</c>, matched per
    /// (scope, action). Storage is already per-action (implied actions are folded
    /// in at write time), so no expansion is needed here. Expired overrides are
    /// excluded, exactly like <c>PermissionService.LoadOverridesAsync</c>.
    /// </summary>
    private async Task<List<Guid>> ResolveRecipientIdsAsync(CancellationToken cancellationToken)
    {
        var roleGrants = await (
            from rp in _db.RolePermissions
            join res in _db.Resources on rp.ResourceId equals res.Id
            join mod in _db.Modules on res.ModuleId equals mod.Id
            where mod.ModuleKey == SyncModuleKey && res.Key == SyncResourceKey
            join sr in _db.StaffRoles on rp.RoleId equals sr.RoleId
            join staff in _db.Staffs on sr.StaffId equals staff.Id
            where staff.IsActive
            select new GrantKey(sr.StaffId, sr.StructureNodeId, sr.StructureNodePath, sr.Year, sr.Semester, rp.Action))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var overrides = await (
            from sp in _db.StaffPermissions
            join res in _db.Resources on sp.ResourceId equals res.Id
            join mod in _db.Modules on res.ModuleId equals mod.Id
            where mod.ModuleKey == SyncModuleKey && res.Key == SyncResourceKey
                && (sp.ExpiresAt == null || sp.ExpiresAt > now)
            join staff in _db.Staffs on sp.StaffId equals staff.Id
            where staff.IsActive
            select new
            {
                Key = new GrantKey(sp.StaffId, sp.StructureNodeId, sp.StructureNodePath, sp.Year, sp.Semester, sp.Action),
                sp.Type,
            })
            .ToListAsync(cancellationToken);

        var allow = new HashSet<GrantKey>(roleGrants);
        foreach (var o in overrides)
        {
            if (o.Type == OverrideType.Allow) allow.Add(o.Key);
        }

        var deny = new HashSet<GrantKey>();
        foreach (var o in overrides)
        {
            if (o.Type == OverrideType.Deny) deny.Add(o.Key);
        }

        allow.ExceptWith(deny);

        return allow.Select(k => k.StaffId).Distinct().ToList();
    }

    // Builds bilingual title/message. LocalizedJson.Of stores {"ar":..,"en":..};
    // the notification read-path decodes it for each recipient's culture.
    private static (string Title, string Message, NotificationType Type) BuildContent(SyncOutcomeNotice n)
    {
        var module = n.ModuleName;
        var direction = n.Direction.ToString();

        if (n.Success)
        {
            return (
                LocalizedJson.Of("اكتملت المزامنة", "Sync completed"),
                LocalizedJson.Of(
                    $"اكتملت مزامنة {module} ({direction}). تمت معالجة {n.RecordsProcessed} سجل، وفشل {n.RecordsFailed}.",
                    $"Sync for {module} ({direction}) completed: {n.RecordsProcessed} processed, {n.RecordsFailed} failed."),
                NotificationType.Info);
        }

        var error = Truncate(n.Error, MaxErrorChars);
        return (
            LocalizedJson.Of("فشلت المزامنة", "Sync failed"),
            LocalizedJson.Of(
                $"فشلت مزامنة {module} ({direction}). الخطأ: {error}",
                $"Sync for {module} ({direction}) failed. Error: {error}"),
            NotificationType.Warning);
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }
}

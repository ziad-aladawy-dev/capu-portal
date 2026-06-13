using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.Paging;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly CoreDbContext _context;
    private readonly NotificationMapper _mapper;
    private readonly ILocalizationService _localization;
    private readonly IOutbox? _outbox;

    public NotificationService(CoreDbContext context, ILocalizationService localization, IOutbox? outbox = null)
    {
        _context = context;
        _mapper = new NotificationMapper();
        _localization = localization;
        _outbox = outbox;
    }

    /// <summary>
    /// Decode the bilingual <c>Title</c> and <c>Message</c> on a notification
    /// DTO against the current culture. Plain-text rows pass through.
    /// </summary>
    private NotificationDto Localize(NotificationDto dto)
    {
        dto.Title = _localization.Get<string>(dto.Title);
        dto.Message = _localization.Get<string>(dto.Message);
        return dto;
    }

    public async Task CreateNotificationAsync(
        Guid recipientUserId,
        string title,
        string message,
        NotificationType type,
        Guid? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // H5 — Negative idempotency check: when the caller supplied a key, see
        // if a row with the same key already exists. Cheap (covered by the
        // filtered unique index) and avoids the noisy DbUpdateException path
        // in the common replay case.
        if (idempotencyKey.HasValue)
        {
            var alreadyExists = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.IdempotencyKey == idempotencyKey, cancellationToken);
            if (alreadyExists) return;
        }

        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            Title = LocalizedJson.Normalize(title),
            Message = LocalizedJson.Normalize(message),
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey,
        };

        _context.Notifications.Add(notification);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (idempotencyKey.HasValue && IsIdempotencyDuplicate(ex))
        {
            // A concurrent dispatcher tick won the race between the AnyAsync
            // probe above and SaveChanges. Treat as a successful replay so the
            // outbox marks the message processed and stops retrying.
            _context.Entry(notification).State = EntityState.Detached;
        }
    }

    private const string IdempotencyIndexFragment = "IdempotencyKey";

    private static bool IsIdempotencyDuplicate(DbUpdateException ex)
    {
        // SQL Server: 2627 = unique constraint, 2601 = unique index violation.
        // Narrowed to errors that mention the IdempotencyKey index so unrelated
        // unique constraints don't get swallowed here.
        if (ex.InnerException is not Microsoft.Data.SqlClient.SqlException sql) return false;
        if (sql.Number != 2627 && sql.Number != 2601) return false;
        return sql.Message.Contains(IdempotencyIndexFragment, StringComparison.OrdinalIgnoreCase);
    }

    public Task EnqueueNotificationAsync(Guid recipientUserId, string title, string message, NotificationType type, CancellationToken cancellationToken = default)
    {
        if (_outbox is null)
        {
            throw new InvalidOperationException(
                "EnqueueNotificationAsync requires IOutbox to be registered. " +
                "Verify AddCoreServices was called — the outbox + dispatcher are wired there.");
        }

        var payload = new NotificationOutboxHandler.NotificationPayload(
            recipientUserId, 
            LocalizedJson.Normalize(title), 
            LocalizedJson.Normalize(message), 
            type);

        // Outbox stages the row on this DbContext; the caller's SaveChangesAsync
        // commits the notification atomically with whatever business state they're
        // saving alongside it.
        return _outbox.EnqueueAsync(NotificationOutboxHandler.TypeKey, payload, cancellationToken);
    }


    public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(Guid userId)
    {
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync();

        return notifications.Select(n => Localize(_mapper.MapToDto(n)));
    }

    public async Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(Guid userId)
    {
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return notifications.Select(n => Localize(_mapper.MapToDto(n)));
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);

        if (notification != null && notification.RecipientUserId == userId && !notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> MarkManyAsReadAsync(IReadOnlyList<Guid> notificationIds, Guid userId, CancellationToken cancellationToken = default)
    {
        if (notificationIds is null || notificationIds.Count == 0) return 0;

        var ids = notificationIds.Distinct().ToArray();

        return await _context.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead && ids.Contains(n.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(n => n.IsRead, true),
                cancellationToken);
    }

    private static readonly HashSet<string> NotificationSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt"
    };

    public async Task<PagedResult<NotificationDto>> SearchAsync(Guid userId, NotificationSearchQuery query, CancellationToken cancellationToken = default)
    {
        var q = _context.Notifications.AsNoTracking().Where(n => n.RecipientUserId == userId);

        if (query.IsRead.HasValue) q = q.Where(n => n.IsRead == query.IsRead.Value);
        if (query.Type.HasValue) q = q.Where(n => n.Type == query.Type.Value);
        if (query.From.HasValue) q = q.Where(n => n.CreatedAt >= query.From.Value);
        if (query.To.HasValue) q = q.Where(n => n.CreatedAt < query.To.Value);

        // Free-text matches against the raw JSON-encoded Title/Message — surface
        // hits regardless of culture. Acceptable for inbox triage volume.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(n => n.Title.Contains(s) || n.Message.Contains(s));
        }

        // Sort whitelist is a single field; validate to surface typos as 400.
        SortClause.Parse(query.Sort, NotificationSortFields);
        q = q.OrderByDescending(n => n.CreatedAt);

        var total = await q.CountAsync(cancellationToken);
        var rows = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>
        {
            Items = rows.Select(n => Localize(_mapper.MapToDto(n))).ToList(),
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.NormalizedPageSize),
        };
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var count = await _context.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(n => n.IsRead, true),
                cancellationToken);

        return count;
    }
}


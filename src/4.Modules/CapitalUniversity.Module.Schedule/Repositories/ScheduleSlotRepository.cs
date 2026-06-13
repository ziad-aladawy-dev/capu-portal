using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.Paging;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;
using CapitalUniversity.Modules.Schedule.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Modules.Schedule.Repositories;

public class ScheduleSlotRepository : IScheduleSlotRepository
{
    private readonly CoreDbContext _context;

    public ScheduleSlotRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<ScheduleSlot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Set<ScheduleSlot>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ScheduleSlot>> GetForOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default) =>
        await _context.Set<ScheduleSlot>()
            .AsNoTracking()
            .Where(s => s.CourseOfferingId == courseOfferingId)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ScheduleSlot>> GetForOfferingsAsync(IReadOnlyList<Guid> courseOfferingIds, CancellationToken cancellationToken = default)
    {
        if (courseOfferingIds.Count == 0) return Array.Empty<ScheduleSlot>();
        return await _context.Set<ScheduleSlot>()
            .AsNoTracking()
            .Where(s => courseOfferingIds.Contains(s.CourseOfferingId))
            .OrderBy(s => s.CourseOfferingId).ThenBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);
    }

    private static readonly HashSet<string> SlotSortFields = new(StringComparer.OrdinalIgnoreCase) { "dayOfWeek", "startTime", "createdAt" };

    public async Task<PagedResult<ScheduleSlot>> SearchAsync(ScheduleSlotSearchQuery query, CancellationToken cancellationToken = default)
    {
        var q = _context.Set<ScheduleSlot>().AsNoTracking().AsQueryable();

        if (query.CourseOfferingId.HasValue) q = q.Where(s => s.CourseOfferingId == query.CourseOfferingId.Value);
        if (query.DayOfWeek.HasValue) q = q.Where(s => s.DayOfWeek == query.DayOfWeek.Value);
        if (query.Kind.HasValue) q = q.Where(s => s.Kind == query.Kind.Value);
        if (query.From.HasValue) q = q.Where(s => s.StartTime >= query.From.Value);
        if (query.To.HasValue) q = q.Where(s => s.StartTime < query.To.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Location != null && x.Location.Contains(s));
        }

        var sort = SortClause.Parse(query.Sort, SlotSortFields);
        IOrderedQueryable<ScheduleSlot>? ord = null;
        if (sort.Count == 0)
        {
            ord = q.OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime);
        }
        else
        {
            foreach (var c in sort)
            {
                ord = (c.Field.ToLowerInvariant(), c.Descending, ord) switch
                {
                    ("dayofweek", true, null) => q.OrderByDescending(s => s.DayOfWeek),
                    ("dayofweek", false, null) => q.OrderBy(s => s.DayOfWeek),
                    ("starttime", true, null) => q.OrderByDescending(s => s.StartTime),
                    ("starttime", false, null) => q.OrderBy(s => s.StartTime),
                    ("createdat", true, null) => q.OrderByDescending(s => s.CreatedAt),
                    ("createdat", false, null) => q.OrderBy(s => s.CreatedAt),
                    ("dayofweek", true, _) => ord!.ThenByDescending(s => s.DayOfWeek),
                    ("dayofweek", false, _) => ord!.ThenBy(s => s.DayOfWeek),
                    ("starttime", true, _) => ord!.ThenByDescending(s => s.StartTime),
                    ("starttime", false, _) => ord!.ThenBy(s => s.StartTime),
                    ("createdat", true, _) => ord!.ThenByDescending(s => s.CreatedAt),
                    ("createdat", false, _) => ord!.ThenBy(s => s.CreatedAt),
                    _ => ord,
                };
            }
        }

        q = ord ?? q.OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime);

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ScheduleSlot>
        {
            Items = items,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.NormalizedPageSize),
        };
    }

    public Task<bool> ExistsAsync(Guid courseOfferingId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end, CancellationToken cancellationToken = default) =>
        _context.Set<ScheduleSlot>()
            .AsNoTracking()
            .AnyAsync(
                s => s.CourseOfferingId == courseOfferingId
                  && s.DayOfWeek == dayOfWeek
                  && s.StartTime == start
                  && s.EndTime == end,
                cancellationToken);

    public Task<bool> HasConflictAsync(
        Guid courseOfferingId,
        DayOfWeek dayOfWeek,
        TimeOnly start,
        TimeOnly end,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        // Classic half-open interval overlap: existing.Start < proposed.End
        // AND existing.End > proposed.Start. Strict inequalities so
        // adjacency (existing.End == start) does not register as a conflict.
        // The (CourseOfferingId, DayOfWeek, StartTime) index already covers
        // the predicate's leading equality filters, so the database scans a
        // narrow range per call — no extra index needed.
        var query = _context.Set<ScheduleSlot>()
            .AsNoTracking()
            .Where(s => s.CourseOfferingId == courseOfferingId
                     && s.DayOfWeek == dayOfWeek
                     && s.StartTime < end
                     && s.EndTime > start);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(ScheduleSlot slot, CancellationToken cancellationToken = default) =>
        await _context.Set<ScheduleSlot>().AddAsync(slot, cancellationToken);

    public void Update(ScheduleSlot slot) => _context.Set<ScheduleSlot>().Update(slot);

    public void Delete(ScheduleSlot slot) => _context.Set<ScheduleSlot>().Remove(slot);

    public async Task<int> DeleteForOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default)
    {
        // M2 — issued as a single SQL DELETE on relational providers via
        // ExecuteDeleteAsync (translates to DELETE … WHERE CourseOfferingId =
        // @id, no client-side hydration). On the InMemory provider this
        // translates to a fetch + per-row remove, which is fine in tests.
        if (_context.Database.IsRelational())
        {
            return await _context.Set<ScheduleSlot>()
                .Where(s => s.CourseOfferingId == courseOfferingId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var rows = await _context.Set<ScheduleSlot>()
            .Where(s => s.CourseOfferingId == courseOfferingId)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return 0;
        _context.Set<ScheduleSlot>().RemoveRange(rows);
        await _context.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }
}

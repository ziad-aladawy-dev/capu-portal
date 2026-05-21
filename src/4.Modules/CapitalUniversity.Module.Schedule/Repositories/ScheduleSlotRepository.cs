using CapitalUniversity.Core.Infrastructure.Persistence;
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
        _context.Set<ScheduleSlot>().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ScheduleSlot>> GetForOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default) =>
        await _context.Set<ScheduleSlot>()
            .AsNoTracking()
            .Where(s => s.CourseOfferingId == courseOfferingId)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid courseOfferingId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end, CancellationToken cancellationToken = default) =>
        _context.Set<ScheduleSlot>()
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
}

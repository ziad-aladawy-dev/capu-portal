using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.Paging;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using Microsoft.EntityFrameworkCore;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;

namespace CapitalUniversity.Modules.CourseOffering.Repositories;

public class CourseOfferingRepository : ICourseOfferingRepository
{
    private readonly CoreDbContext _context;

    public CourseOfferingRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<CourseOfferingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Set<CourseOfferingEntity>().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CourseOfferingEntity>> GetForNodeSemesterAsync(
        Guid structureNodeId,
        Guid semesterId,
        OfferingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<CourseOfferingEntity>()
            .AsNoTracking()
            .Where(o => o.StructureNodeId == structureNodeId && o.SemesterId == semesterId);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        return await query
            .OrderBy(o => o.CourseId).ThenBy(o => o.SectionCode)
            .ToListAsync(cancellationToken);
    }

    private static readonly HashSet<string> OfferingSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt", "sectionCode", "status"
    };

    public async Task<PagedResult<CourseOfferingEntity>> SearchAsync(CourseOfferingSearchQuery query, CancellationToken cancellationToken = default)
    {
        var q = _context.Set<CourseOfferingEntity>().AsNoTracking().AsQueryable();

        if (query.SemesterId.HasValue) q = q.Where(o => o.SemesterId == query.SemesterId.Value);
        if (query.StructureNodeId.HasValue) q = q.Where(o => o.StructureNodeId == query.StructureNodeId.Value);
        if (query.CourseId.HasValue) q = q.Where(o => o.CourseId == query.CourseId.Value);
        if (query.Status.HasValue) q = q.Where(o => o.Status == query.Status.Value);
        if (query.RegistrationState.HasValue) q = q.Where(o => o.RegistrationState == query.RegistrationState.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(o => o.SectionCode.Contains(s));
        }

        var sort = SortClause.Parse(query.Sort, OfferingSortFields);
        if (sort.Count == 0)
        {
            q = q.OrderByDescending(o => o.CreatedAt);
        }
        else
        {
            IOrderedQueryable<CourseOfferingEntity>? ord = null;
            foreach (var c in sort)
            {
                ord = (c.Field.ToLowerInvariant(), c.Descending, ord) switch
                {
                    ("createdat", true, null)  => q.OrderByDescending(o => o.CreatedAt),
                    ("createdat", false, null) => q.OrderBy(o => o.CreatedAt),
                    ("sectioncode", true, null)  => q.OrderByDescending(o => o.SectionCode),
                    ("sectioncode", false, null) => q.OrderBy(o => o.SectionCode),
                    ("status", true, null)  => q.OrderByDescending(o => o.Status),
                    ("status", false, null) => q.OrderBy(o => o.Status),
                    ("createdat", true, _)  => ord!.ThenByDescending(o => o.CreatedAt),
                    ("createdat", false, _) => ord!.ThenBy(o => o.CreatedAt),
                    ("sectioncode", true, _)  => ord!.ThenByDescending(o => o.SectionCode),
                    ("sectioncode", false, _) => ord!.ThenBy(o => o.SectionCode),
                    ("status", true, _)  => ord!.ThenByDescending(o => o.Status),
                    ("status", false, _) => ord!.ThenBy(o => o.Status),
                    _ => ord,
                };
            }
            q = ord ?? q.OrderByDescending(o => o.CreatedAt);
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CourseOfferingEntity>
        {
            Items = items,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.NormalizedPageSize),
        };
    }

    public async Task<IReadOnlyList<CourseOfferingEntity>> GetForCourseAsync(
        Guid courseId,
        Guid semesterId,
        CancellationToken cancellationToken = default) =>
        await _context.Set<CourseOfferingEntity>()
            .AsNoTracking()
            .Where(o => o.CourseId == courseId && o.SemesterId == semesterId)
            .OrderBy(o => o.StructureNodeId).ThenBy(o => o.SectionCode)
            .ToListAsync(cancellationToken);

    public Task<bool> SectionExistsAsync(Guid courseId, Guid semesterId, Guid structureNodeId, string sectionCode, CancellationToken cancellationToken = default) =>
        _context.Set<CourseOfferingEntity>()
            .AnyAsync(
                o => o.CourseId == courseId
                  && o.SemesterId == semesterId
                  && o.StructureNodeId == structureNodeId
                  && o.SectionCode == sectionCode,
                cancellationToken);

    // Bounded retry budget for the Try* primitives below. Three attempts is
    // enough to handle the realistic contention rate for this project (admin
    // edits + low-frequency registration writes) without risking a hot loop
    // under sustained contention — the caller observes false and decides.
    private const int RegistrationUpdateMaxAttempts = 3;

    public async Task<bool> TryIncrementRegistrationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Load → guard → SaveChanges with optimistic concurrency on RowVersion.
        // The guards are evaluated in-memory; if a concurrent writer beats us,
        // SaveChanges throws DbUpdateConcurrencyException, we re-fetch and
        // re-evaluate. Two callers racing for the last seat: only one
        // SaveChanges succeeds; the other reloads, sees RegisteredCount ==
        // Capacity, and returns false. No over-increment is possible.
        for (var attempt = 0; attempt < RegistrationUpdateMaxAttempts; attempt++)
        {
            // H9 — drop everything tracked so the next FirstOrDefaultAsync
            // identity-resolves to a freshly loaded entity. The previous
            // Entry(offering).State = Detached only detached the row we just
            // mutated; any sibling entity left in Modified state by a caller
            // higher up the stack would otherwise tag along on the retry
            // SaveChanges. ChangeTracker.Clear isolates the registration
            // increment so the retry is truly idempotent.
            _context.ChangeTracker.Clear();

            var offering = await _context.Set<CourseOfferingEntity>()
                .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
            if (offering is null) return false;
            if (offering.Status == OfferingStatus.Cancelled) return false;
            if (offering.RegisteredCount >= offering.Capacity) return false;

            offering.IncrementRegistration();
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Loop. The ChangeTracker.Clear at the top of the next
                // iteration drops the stale tracker state in one shot.
            }
        }
        // Exhausted retries under sustained contention — caller decides what
        // to do (typically: surface as a 409 / "try again").
        return false;
    }

    public async Task<bool> TryDecrementRegistrationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < RegistrationUpdateMaxAttempts; attempt++)
        {
            _context.ChangeTracker.Clear();

            var offering = await _context.Set<CourseOfferingEntity>()
                .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
            if (offering is null) return false;
            if (offering.RegisteredCount == 0) return false;

            offering.DecrementRegistration();
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // See sibling Try* method above.
            }
        }
        return false;
    }

    public async Task AddAsync(CourseOfferingEntity offering, CancellationToken cancellationToken = default) =>
        await _context.Set<CourseOfferingEntity>().AddAsync(offering, cancellationToken);

    public void Update(CourseOfferingEntity offering) => _context.Set<CourseOfferingEntity>().Update(offering);

    public void Delete(CourseOfferingEntity offering) => _context.Set<CourseOfferingEntity>().Remove(offering);
}

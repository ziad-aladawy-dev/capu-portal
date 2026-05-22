using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
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
                // Detach the stale snapshot so the next iteration's
                // FirstOrDefaultAsync re-reads from the database.
                _context.Entry(offering).State = EntityState.Detached;
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
                _context.Entry(offering).State = EntityState.Detached;
            }
        }
        return false;
    }

    public async Task AddAsync(CourseOfferingEntity offering, CancellationToken cancellationToken = default) =>
        await _context.Set<CourseOfferingEntity>().AddAsync(offering, cancellationToken);

    public void Update(CourseOfferingEntity offering) => _context.Set<CourseOfferingEntity>().Update(offering);

    public void Delete(CourseOfferingEntity offering) => _context.Set<CourseOfferingEntity>().Remove(offering);
}

using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Modules.StudentServices.Repositories;

public class StudentServiceRepository : IStudentServiceRepository
{
    private readonly CoreDbContext _context;

    public StudentServiceRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<StudentService?> GetByIdAsync(Guid id, bool includeChildren = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<StudentService>().AsQueryable();
        if (includeChildren)
        {
            query = query.Include(s => s.Fields).Include(s => s.Documents);
        }
        return query.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public Task<StudentService?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Set<StudentService>()
            .Include(s => s.Fields)
            .Include(s => s.Documents)
            .FirstOrDefaultAsync(s => s.Code == code, cancellationToken);

    public async Task<(IReadOnlyList<StudentService> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<StudentService>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            // Substring search on Code; Name is bilingual JSON so a naive
            // search on it would return false positives — Code is the
            // canonical search field for admin tooling.
            query = query.Where(x => EF.Functions.Like(x.Code, $"%{s}%") || EF.Functions.Like(x.Name, $"%{s}%"));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(Math.Max(0, page - 1) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<StudentService>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<StudentService>()
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<StudentService>().Where(s => s.Code == code);
        if (excludeId.HasValue) query = query.Where(s => s.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(StudentService service, CancellationToken cancellationToken = default) =>
        await _context.Set<StudentService>().AddAsync(service, cancellationToken);

    public void Update(StudentService service) => _context.Set<StudentService>().Update(service);

    public void Delete(StudentService service) => _context.Set<StudentService>().Remove(service);
}

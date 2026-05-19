using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation;
using CapitalUniversity.Modules.Student.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Modules.Student.Repositories;

public class StudentProfileRecordRepository : IStudentProfileRecordRepository
{
    private readonly CoreDbContext _context;

    public StudentProfileRecordRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<StudentProfileRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Set<StudentProfileRecord>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StudentProfileRecord>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await _context.Set<StudentProfileRecord>()
            .AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .OrderBy(r => r.Category)
            .ToListAsync(cancellationToken);

    public Task<StudentProfileRecord?> GetForStudentCategoryAsync(Guid studentId, StudentProfileCategory category, string customCategoryKey, CancellationToken cancellationToken = default) =>
        _context.Set<StudentProfileRecord>()
            .FirstOrDefaultAsync(
                r => r.StudentId == studentId
                  && r.Category == category
                  && r.CustomCategoryKey == customCategoryKey,
                cancellationToken);

    public async Task AddAsync(StudentProfileRecord record, CancellationToken cancellationToken = default) =>
        await _context.Set<StudentProfileRecord>().AddAsync(record, cancellationToken);

    public void Update(StudentProfileRecord record) => _context.Set<StudentProfileRecord>().Update(record);

    public void Delete(StudentProfileRecord record) => _context.Set<StudentProfileRecord>().Remove(record);
}

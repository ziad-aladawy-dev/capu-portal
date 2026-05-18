using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.StudentInformation;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class StudentProfileRecordRepository : IStudentProfileRecordRepository
{
    private readonly CoreDbContext _context;

    public StudentProfileRecordRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<StudentProfileRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.StudentProfileRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StudentProfileRecord>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await _context.StudentProfileRecords
            .AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .OrderBy(r => r.Category)
            .ToListAsync(cancellationToken);

    public Task<StudentProfileRecord?> GetForStudentCategoryAsync(Guid studentId, StudentProfileCategory category, string customCategoryKey, CancellationToken cancellationToken = default) =>
        _context.StudentProfileRecords
            .FirstOrDefaultAsync(
                r => r.StudentId == studentId
                  && r.Category == category
                  && r.CustomCategoryKey == customCategoryKey,
                cancellationToken);

    public async Task AddAsync(StudentProfileRecord record, CancellationToken cancellationToken = default) =>
        await _context.StudentProfileRecords.AddAsync(record, cancellationToken);

    public void Update(StudentProfileRecord record) => _context.StudentProfileRecords.Update(record);

    public void Delete(StudentProfileRecord record) => _context.StudentProfileRecords.Remove(record);
}

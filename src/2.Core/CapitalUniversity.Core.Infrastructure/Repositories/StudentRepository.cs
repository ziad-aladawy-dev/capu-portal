using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly CoreDbContext _context;

    public StudentRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<Student?> GetByIdAsync(Guid id)
    {
        return await _context.Students
            .Include(x => x.StructureNode)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Student>> GetAllAsync()
    {
        return await _context.Students
            .Include(x => x.StructureNode)
            .OrderBy(x => x.StudentCode)
            .ToListAsync();
    }

    public async Task AddAsync(Student student)
    {
        await _context.Students.AddAsync(student);
    }

    public Task UpdateAsync(Student student)
    {
        _context.Students.Update(student);

        return Task.CompletedTask;
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        var student = await GetByIdAsync(id);

        if (student == null)
            return;

        student.IsDeleted = true;
        student.UpdatedAt = DateTime.UtcNow;

        _context.Students.Update(student);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Students
            .AnyAsync(x => x.Id == id);
    }

    public async Task<bool> StudentCodeExistsAsync(string studentCode)
    {
        return await _context.Students
            .AnyAsync(x => x.StudentCode == studentCode);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
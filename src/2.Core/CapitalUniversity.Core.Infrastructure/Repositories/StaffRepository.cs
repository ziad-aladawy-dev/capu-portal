using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class StaffRepository : IStaffRepository
{
    private readonly CoreDbContext _context;

    public StaffRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<Staff?> GetByIdAsync(Guid id)
    {
        return await _context.Staff
            .Include(x => x.StructureNode)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Staff>> GetAllAsync()
    {
        return await _context.Staff
            .Include(x => x.StructureNode)
            .OrderBy(x => x.EmployeeCode)
            .ToListAsync();
    }

    public async Task AddAsync(Staff staff)
    {
        await _context.Staff.AddAsync(staff);
    }

    public Task UpdateAsync(Staff staff)
    {
        _context.Staff.Update(staff);

        return Task.CompletedTask;
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        var staff = await GetByIdAsync(id);

        if (staff == null)
            return;

        staff.IsDeleted = true;
        staff.UpdatedAt = DateTime.UtcNow;

        _context.Staff.Update(staff);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Staff
            .AnyAsync(x => x.Id == id);
    }

    public async Task<bool> EmployeeCodeExistsAsync(
        string employeeCode)
    {
        return await _context.Staff
            .AnyAsync(x =>
                x.EmployeeCode == employeeCode);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
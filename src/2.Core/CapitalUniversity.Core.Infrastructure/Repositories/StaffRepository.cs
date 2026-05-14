
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CapitalUniversity.Core.Abstractions.Repositories;

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
        return await _context.Staffs
            .Include(x => x.StructureNode)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Staff>> GetAllAsync()
    {
        return await _context.Staffs
            .Include(x => x.StructureNode)
            .OrderBy(x => x.EmployeeCode)
            .ToListAsync();
    }

    public async Task AddAsync(Staff staff)
    {
        await _context.Staffs.AddAsync(staff);
    }

    public Task UpdateAsync(Staff staff)
    {
        _context.Staffs.Update(staff);

        return Task.CompletedTask;
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        var staff = await GetByIdAsync(id);

        if (staff == null)
            return;

        staff.IsDeleted = true;
        staff.UpdatedAt = DateTime.UtcNow;

        _context.Staffs.Update(staff);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Staffs
            .AnyAsync(x => x.Id == id);
    }

    public async Task<bool> EmployeeCodeExistsAsync(
        string employeeCode)
    {
        return await _context.Staffs
            .AnyAsync(x =>
                x.EmployeeCode == employeeCode);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

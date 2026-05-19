using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
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
        return await _context.Staffs
            .Include(x => x.StructureNode)
                .ThenInclude(x => x.Parent)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Staff>> GetAllAsync()
    {
        return await _context.Staffs
            .Include(x => x.StructureNode)
                .ThenInclude(x => x.Parent)
            .OrderBy(x => x.EmployeeCode)
            .ToListAsync();
    }

    public async Task<PagedResult<Staff>> SearchAsync(StaffQueryRequest request)
    {
        var query = _context.Staffs
            .Include(x => x.StructureNode)
                .ThenInclude(x => x.Parent)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();

            query = query.Where(x =>
                x.Name.ToLower().Contains(search) ||
                x.EmployeeCode.ToLower().Contains(search) ||
                x.Email.ToLower().Contains(search) ||
                x.NationalId.Contains(search));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(x =>
                x.IsActive ==
                request.IsActive.Value);
        }

        if (request.PasswordExpired.HasValue)
        {
            if (request.PasswordExpired.Value)
            {
                query = query.Where(x =>
                    x.PasswordExpiry.HasValue &&
                    x.PasswordExpiry < DateTime.UtcNow);
            }
            else
            {
                query = query.Where(x =>
                    !x.PasswordExpiry.HasValue ||
                    x.PasswordExpiry >= DateTime.UtcNow);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.Where(x =>
                x.Role == request.Role);
        }

        if (!string.IsNullOrWhiteSpace(request.JobTitle))
        {
            query = query.Where(x =>
                x.JobTitle ==
                request.JobTitle);
        }

        if (request.StructureNodeId.HasValue)
        {
            query = query.Where(x =>
                x.StructureNodeId ==
                request.StructureNodeId.Value);
        }

        if (request.ScopeNodeId.HasValue)
        {
            var scopeNode =
                await _context.StructureNodes
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                        request.ScopeNodeId.Value);

            if (scopeNode != null)
            {
                query = query.Where(x =>
                    x.StructureNode.Path.StartsWith(
                        scopeNode.Path));
            }
        }

        int totalCount =
            await query.CountAsync();

        var items = await query
            .OrderBy(x => x.EmployeeCode)
            .Skip((request.Page - 1)
                * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<Staff>
        {
            Items = items,

            Page = request.Page,

            PageSize = request.PageSize,

            TotalCount = totalCount,

            TotalPages =
                (int)Math.Ceiling(
                    totalCount /
                    (double)request.PageSize)
        };
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

    public async Task ToggleStatusAsync(Guid id)
    {
        var staff = await GetByIdAsync(id);

        if (staff == null)
            return;

        staff.IsActive = !staff.IsActive;

        staff.UpdatedAt = DateTime.UtcNow;

        _context.Staffs.Update(staff);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Staffs
            .AnyAsync(x => x.Id == id);
    }

    public async Task<bool> EmployeeCodeExistsAsync(string employeeCode)
    {
        return await _context.Staffs
            .AnyAsync(x =>
                x.EmployeeCode == employeeCode);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Staffs
            .AnyAsync(x =>
                x.Email == email);
    }

    public async Task<bool> NationalIdExistsAsync(string nationalId)
    {
        return await _context.Staffs
            .AnyAsync(x =>
                x.NationalId == nationalId);
    }

    public async Task<string?> GetLastEmployeeCodeAsync()
    {
        return await _context.Staffs
            .OrderByDescending(x => x.EmployeeCode)
            .Select(x => x.EmployeeCode)
            .FirstOrDefaultAsync();
    }

    // P0.7 — services/UoW own the transaction boundary. This delegate exists
    // for legacy callers; new code MUST go through IUnitOfWork.SaveChangesAsync.
    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
}
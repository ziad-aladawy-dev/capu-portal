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
            .AsNoTracking()
            .Include(x => x.StructureNode)
                .ThenInclude(x => x.Parent)
                    .ThenInclude(x => x!.Parent)
                        .ThenInclude(x => x!.Parent)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Staff>> GetAllAsync()
    {
        return await _context.Staffs
            .AsNoTracking()
            .Include(x => x.StructureNode)
                .ThenInclude(x => x.Parent)
                    .ThenInclude(x => x!.Parent)
                        .ThenInclude(x => x!.Parent)
            .OrderBy(x => x.EmployeeCode)
            .ToListAsync();
    }

    public async Task<PagedResult<StaffDto>> SearchAsync(StaffQueryRequest request)
    {
        var query = _context.Staffs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search;

            query = query.Where(x =>
                x.Name.Contains(search) ||
                x.EmployeeCode.Contains(search) ||
                x.Email.Contains(search) ||
                x.NationalId.Contains(search));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(x =>
                x.IsActive == request.IsActive.Value);
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
                x.JobTitle == request.JobTitle);
        }

        if (request.StructureNodeId.HasValue)
        {
            query = query.Where(x =>
                x.StructureNodeId == request.StructureNodeId.Value);
        }

        if (request.ScopeNodeId.HasValue)
        {
            var scopeNode =
                await _context.StructureNodes
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.Id == request.ScopeNodeId.Value);

            if (scopeNode != null)
            {
                query = query.Where(x =>
                    x.StructureNode.Path.StartsWith(scopeNode.Path));
            }
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(x => x.EmployeeCode)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new StaffDto
            {
                Id = x.Id,
                EmployeeCode = x.EmployeeCode,
                Name = x.Name,
                Email = x.Email,
                NationalId = x.NationalId,
                PhoneNumber = x.PhoneNumber,
                BirthDate = x.BirthDate,
                IsActive = x.IsActive,
                PasswordExpiry = x.PasswordExpiry,
                CreatedAt = x.CreatedAt,
                Role = x.Role,
                JobTitle = x.JobTitle,
                StructureNodeName = x.StructureNode.Name,
                FacultyName = (x.StructureNode.Parent != null && x.StructureNode.Parent.Parent != null) ? x.StructureNode.Parent.Parent.Name : string.Empty
            })
            .ToListAsync();

        return new PagedResult<StaffDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
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

    public async Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request)
    {
        var query = _context.Staffs.AsNoTracking();

        if (request.ScopeNodeId.HasValue)
        {
            var scopeNode = await _context.StructureNodes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ScopeNodeId.Value);

            if (scopeNode != null)
                query = query.Where(x => x.StructureNode.Path.StartsWith(scopeNode.Path));
        }

        var counts = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(x => x.IsActive),
                Inactive = g.Count(x => !x.IsActive)
            })
            .FirstOrDefaultAsync();

        return new UserStatisticsDto
        {
            TotalStaff = counts?.Total ?? 0,
            ActiveStaff = counts?.Active ?? 0,
            InactiveStaff = counts?.Inactive ?? 0
        };
    }

    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
}
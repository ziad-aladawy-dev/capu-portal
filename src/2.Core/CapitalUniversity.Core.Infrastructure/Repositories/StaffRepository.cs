using System.Linq.Expressions;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Domain.UniversityStructure;
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
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IReadOnlyList<Staff>> GetRangeAsync(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0) return Array.Empty<Staff>();
        return await _context.Staffs
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(StaffWithAncestors)
            .ToListAsync();
    }

    public async Task<List<Staff>> GetAllAsync()
    {
        return await _context.Staffs
            .AsNoTracking()
            .Select(StaffWithAncestors)
            .OrderBy(x => x.EmployeeCode)
            .ToListAsync();
    }

    public async Task<PagedResult<Staff>> SearchAsync(StaffQueryRequest request)
    {
        var query = _context.Staffs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(x =>
                x.Name.Contains(search) ||
                x.EmployeeCode.Contains(search) ||
                x.Email.Contains(search) ||
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
                    .AsNoTracking()
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
            .AsNoTracking()
            .Select(StaffWithAncestors)
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

    public async Task AddRangeAsync(IReadOnlyList<Staff> staffList)
    {
        await _context.Staffs.AddRangeAsync(staffList);
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
            .AsNoTracking()
            .AnyAsync(x => x.Id == id);
    }

    public async Task<bool> EmployeeCodeExistsAsync(string employeeCode)
    {
        return await _context.Staffs
            .AsNoTracking()
            .AnyAsync(x =>
                x.EmployeeCode == employeeCode);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Staffs
            .AsNoTracking()
            .AnyAsync(x =>
                x.Email == email);
    }

    public async Task<bool> NationalIdExistsAsync(string nationalId)
    {
        return await _context.Staffs
            .AsNoTracking()
            .AnyAsync(x =>
                x.NationalId == nationalId);
    }

    public async Task<string?> GetLastEmployeeCodeAsync()
    {
        return await _context.Staffs
            .AsNoTracking()
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

        var stats = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalStaff = g.Count(),
                ActiveStaff = g.Count(x => x.IsActive),
                InactiveStaff = g.Count(x => !x.IsActive)
            })
            .FirstOrDefaultAsync();

        return stats is null
            ? new UserStatisticsDto()
            : new UserStatisticsDto
            {
                TotalStaff = stats.TotalStaff,
                ActiveStaff = stats.ActiveStaff,
                InactiveStaff = stats.InactiveStaff
            };
    }

    // P0.7 — services/UoW own the transaction boundary. This delegate exists
    // for legacy callers; new code MUST go through IUnitOfWork.SaveChangesAsync.
    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();

    // Select projection that loads ONLY the ancestor fields consumed by
    // StaffService.Map() — no unused Depth/Order/IsActive/Children columns.
    private static readonly Expression<Func<Staff, Staff>> StaffWithAncestors = s => new Staff
    {
        Id = s.Id,
        EmployeeCode = s.EmployeeCode,
        Name = s.Name,
        NationalId = s.NationalId,
        BirthDate = s.BirthDate,
        PhoneNumber = s.PhoneNumber,
        Email = s.Email,
        Role = s.Role,
        JobTitle = s.JobTitle,
        PhotoUrl = s.PhotoUrl,
        Gender = s.Gender,
        Qualification = s.Qualification,
        StructureNodeId = s.StructureNodeId,
        IsActive = s.IsActive,
        PasswordExpiry = s.PasswordExpiry,
        CreatedAt = s.CreatedAt,
        StructureNode = s.StructureNode == null ? null : new StructureNode
        {
            Id = s.StructureNode.Id,
            Name = s.StructureNode.Name,
            Type = s.StructureNode.Type,
            Path = s.StructureNode.Path,
            Parent = s.StructureNode.Parent == null ? null : new StructureNode
            {
                Id = s.StructureNode.Parent.Id,
                Name = s.StructureNode.Parent.Name,
                Type = s.StructureNode.Parent.Type,
                Path = s.StructureNode.Parent.Path,
                Parent = s.StructureNode.Parent.Parent == null ? null : new StructureNode
                {
                    Id = s.StructureNode.Parent.Parent.Id,
                    Name = s.StructureNode.Parent.Parent.Name,
                    Type = s.StructureNode.Parent.Parent.Type,
                    Path = s.StructureNode.Parent.Parent.Path,
                    Parent = s.StructureNode.Parent.Parent.Parent == null ? null : new StructureNode
                    {
                        Id = s.StructureNode.Parent.Parent.Parent.Id,
                        Name = s.StructureNode.Parent.Parent.Parent.Name,
                        Type = s.StructureNode.Parent.Parent.Parent.Type,
                        Path = s.StructureNode.Parent.Parent.Parent.Path,
                    }
                }
            }
        }
    };
}

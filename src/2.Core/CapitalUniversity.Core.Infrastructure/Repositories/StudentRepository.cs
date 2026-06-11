using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
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
                .ThenInclude(x => x.Parent)
                    .ThenInclude(x => x.Parent)
                        .ThenInclude(x => x.Parent)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Student>> GetAllAsync()
    {
        return await _context.Students
            .Include(x => x.StructureNode)
                .ThenInclude(x => x.Parent)
                    .ThenInclude(x => x!.Parent)
                        .ThenInclude(x => x!.Parent)
            .OrderBy(x => x.StudentCode)
            .ToListAsync();
    }

    public async Task<PagedResult<Student>> SearchAsync(StudentQueryRequest request)
    {
        var query = _context.Students.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();

            query = query.Where(x =>
                x.Name.ToLower().Contains(search) ||
                x.StudentCode.ToLower().Contains(search) ||
                x.Email.ToLower().Contains(search) ||
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

        if (request.LevelId.HasValue)
        {
            query = query.Where(x =>
                x.StructureNodeId ==
                request.LevelId.Value);
        }

        if (request.ProgramId.HasValue)
        {
            query = query.Where(x =>
                x.StructureNode.ParentId ==
                request.ProgramId.Value);
        }

        if (request.FacultyId.HasValue)
        {
            var facultyNode =
                await _context.StructureNodes
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                        request.FacultyId.Value);

            if (facultyNode != null)
            {
                query = query.Where(x =>
                    x.StructureNode.Path.StartsWith(
                        facultyNode.Path));
            }
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

        query = query
            .Include(x => x.StructureNode)
                .ThenInclude(x => x.Parent)
                    .ThenInclude(x => x!.Parent)
                        .ThenInclude(x => x!.Parent);

        var items = await query
            .OrderBy(x => x.StudentCode)
            .Skip((request.Page - 1)
                * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<Student>
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

    public async Task ToggleStatusAsync(Guid id)
    {
        var student = await GetByIdAsync(id);

        if (student == null)
            return;

        student.IsActive = !student.IsActive;

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

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Students
            .AnyAsync(x =>
                x.Email == email);
    }

    public async Task<bool> NationalIdExistsAsync(string nationalId)
    {
        return await _context.Students
            .AnyAsync(x =>
                x.NationalId == nationalId);
    }

    public async Task<string?> GetLastStudentCodeAsync()
    {
        return await _context.Students
            .OrderByDescending(x => x.StudentCode)
            .Select(x => x.StudentCode)
            .FirstOrDefaultAsync();
    }

    public async Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request)
    {
        var query = _context.Students.AsQueryable();

        if (request.ScopeNodeId.HasValue)
        {
            var scopeNode = await _context.StructureNodes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == request.ScopeNodeId.Value);

            if (scopeNode != null)
                query = query.Where(x => x.StructureNode.Path.StartsWith(scopeNode.Path));
        }

        return new UserStatisticsDto
        {
            TotalStudents = await query.CountAsync(),
            ActiveStudents = await query.CountAsync(x => x.IsActive),
            InactiveStudents = await query.CountAsync(x => !x.IsActive)
        };
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
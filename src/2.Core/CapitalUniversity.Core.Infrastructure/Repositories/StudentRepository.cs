using System.Linq.Expressions;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
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
            .AsNoTracking()
            .Include(x => x.StructureNode)
                .ThenInclude(x => x.Parent)
                    .ThenInclude(x => x.Parent)
                        .ThenInclude(x => x.Parent)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Student>> GetAllAsync()
    {
        return await _context.Students
            .AsNoTracking()
            .Select(StudentWithAncestors)
            .OrderBy(x => x.StudentCode)
            .ToListAsync();
    }

    public async Task<PagedResult<Student>>  SearchAsync(StudentQueryRequest request)
    {
        var query = _context.Students.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(x =>
                x.Name.Contains(search) ||
                x.StudentCode.Contains(search) ||
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

        if (request.LevelId.HasValue)
        {
            query = query.Where(x =>
                x.StructureNodeId ==
                request.LevelId.Value);
        }

        if (request.LevelOrder.HasValue)
        {
            query = query.Where(x =>
                x.StructureNode.Type == StructureNodeType.Level &&
                x.StructureNode.Order == request.LevelOrder.Value);
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
                    .AsNoTracking()
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

        query = query
            .AsNoTracking()
            .Select(StudentWithAncestors);

        IOrderedQueryable<Student> orderedQuery;

        switch (request.SortBy?.ToLower())
        {
            case "name":
                orderedQuery = request.Ascending
                    ? query.OrderBy(x => x.Name)
                    : query.OrderByDescending(x => x.Name);
                break;
            case "email":
                orderedQuery = request.Ascending
                    ? query.OrderBy(x => x.Email)
                    : query.OrderByDescending(x => x.Email);
                break;
            case "createdat":
                orderedQuery = request.Ascending
                    ? query.OrderBy(x => x.CreatedAt)
                    : query.OrderByDescending(x => x.CreatedAt);
                break;
            case "studentcode":
            default:
                orderedQuery = request.Ascending
                    ? query.OrderBy(x => x.StudentCode)
                    : query.OrderByDescending(x => x.StudentCode);
                break;
        }

        var items = await orderedQuery
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

    public async Task AddRangeAsync(IReadOnlyList<Student> students)
    {
        await _context.Students.AddRangeAsync(students);
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
            .AsNoTracking()
            .AnyAsync(x => x.Id == id);
    }

    public async Task<bool> StudentCodeExistsAsync(string studentCode)
    {
        return await _context.Students
            .AsNoTracking()
            .AnyAsync(x => x.StudentCode == studentCode);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Students
            .AsNoTracking()
            .AnyAsync(x =>
                x.Email == email);
    }

    public async Task<bool> NationalIdExistsAsync(string nationalId)
    {
        return await _context.Students
            .AsNoTracking()
            .AnyAsync(x =>
                x.NationalId == nationalId);
    }

    public async Task<string?> GetLastStudentCodeAsync()
    {
        return await _context.Students
            .AsNoTracking()
            .OrderByDescending(x => x.StudentCode)
            .Select(x => x.StudentCode)
            .FirstOrDefaultAsync();
    }

    public async Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request)
    {
        var query = _context.Students.AsNoTracking();

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
                TotalStudents = g.Count(),
                ActiveStudents = g.Count(x => x.IsActive),
                InactiveStudents = g.Count(x => !x.IsActive)
            })
            .FirstOrDefaultAsync();

        return stats is null
            ? new UserStatisticsDto()
            : new UserStatisticsDto
            {
                TotalStudents = stats.TotalStudents,
                ActiveStudents = stats.ActiveStudents,
                InactiveStudents = stats.InactiveStudents
            };
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    // Select projection that loads ONLY the ancestor fields consumed by
    // StudentService.Map() — no unused Depth/Order/IsActive/Children columns.
    private static readonly Expression<Func<Student, Student>> StudentWithAncestors = s => new Student
    {
        Id = s.Id,
        StudentCode = s.StudentCode,
        Name = s.Name,
        NationalId = s.NationalId,
        BirthDate = s.BirthDate,
        PhoneNumber = s.PhoneNumber,
        Email = s.Email,
        PhotoUrl = s.PhotoUrl,
        Gender = s.Gender,
        GuardianName = s.GuardianName,
        GuardianPhone = s.GuardianPhone,
        StructureNodeId = s.StructureNodeId,
        IsActive = s.IsActive,
        PasswordExpiry = s.PasswordExpiry,
        CreatedAt = s.CreatedAt,
        ExternallySourced = new ExternallySourcedData { ExternalId = s.ExternallySourced.ExternalId },
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

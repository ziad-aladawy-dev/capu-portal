using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.StaffManagement;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Application.StaffManagement;

public class StaffService : IStaffService
{
    private readonly IStaffRepository _repository;

    private readonly IStructureNodeRepository
        _structureRepository;

    private readonly IPasswordHasher _passwordHasher;

    public StaffService(
        IStaffRepository repository,
        IStructureNodeRepository structureRepository,
        IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _structureRepository = structureRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Guid> CreateAsync(CreateStaffRequest request)
    {
        if (await _repository.EmailExistsAsync(
                    request.Email))
        {
            throw new Exception(
                "Email already exists");
        }

        if (await _repository
            .NationalIdExistsAsync(
                request.NationalId))
        {
            throw new Exception(
                "National ID already exists");
        }

        if (request.Password !=
            request.ConfirmPassword)
        {
            throw new Exception(
                "Passwords do not match");
        }

        var hashedPassword = _passwordHasher.HashPassword(request.Password);

        var structureNode = await _structureRepository
            .GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new Exception(
                "Structure node not found");
        }

        if (string.IsNullOrWhiteSpace(
           request.EmployeeCode))
        {
            request.EmployeeCode =
                await GenerateEmployeeCodeAsync();
        }

        bool codeExists =
           await _repository
               .EmployeeCodeExistsAsync(
                   request.EmployeeCode);

        if (codeExists)
        {
            throw new Exception(
                "Employee code already exists");
        }

        var staff = new Staff
        {
            Id = Guid.NewGuid(),

            EmployeeCode = request.EmployeeCode,

            PasswordHash = hashedPassword,

            Name = request.Name,

            NationalId = request.NationalId,

            BirthDate = request.BirthDate,

            PhoneNumber = request.PhoneNumber,

            Email = request.Email,

            Role = request.Role,

            JobTitle = request.JobTitle,

            StructureNodeId =
                request.StructureNodeId,

            PasswordExpiry =
                request.PasswordExpiry,

            IsActive = true
        };

        await _repository.AddAsync(staff);

        await _repository.SaveChangesAsync();

        return staff.Id;
    }

    public async Task UpdateAsync(
        Guid id,
        UpdateStaffRequest request)
    {
        var staff = await _repository
            .GetByIdAsync(id);

        if (staff == null)
            throw new Exception("Staff not found");

        var structureNode = await _structureRepository
            .GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new Exception(
                "Structure node not found");
        }

        bool emailExists =
            await _repository
                .EmailExistsAsync(
                    request.Email);

        if (emailExists &&
            staff.Email != request.Email)
        {
            throw new Exception(
                "Email already exists");
        }

        bool nationalIdExists =
            await _repository
                .NationalIdExistsAsync(
                    request.NationalId);

        if (nationalIdExists &&
            staff.NationalId !=
            request.NationalId)
        {
            throw new Exception(
                "National ID already exists");
        }

        if (!string.IsNullOrWhiteSpace(
            request.Password))
        {
            if (request.Password !=
                request.ConfirmPassword)
            {
                throw new Exception(
                    "Passwords do not match");
            }

            staff.PasswordHash = _passwordHasher.HashPassword(request.Password);
        }

        staff.Name = request.Name;

        staff.NationalId = request.NationalId;

        staff.BirthDate = request.BirthDate;

        staff.PhoneNumber = request.PhoneNumber;

        staff.Email = request.Email;

        staff.Role = request.Role;

        staff.JobTitle = request.JobTitle;

        staff.StructureNodeId = request.StructureNodeId;

        staff.IsActive = request.IsActive;

        staff.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(staff);

        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        bool exists = await _repository
            .ExistsAsync(id);

        if (!exists)
            throw new Exception("Staff not found");

        await _repository.SoftDeleteAsync(id);

        await _repository.SaveChangesAsync();
    }

    public async Task ToggleStatusAsync(Guid id)
    {
        bool exists = await _repository
            .ExistsAsync(id);

        if (!exists)
        {
            throw new Exception(
                "Staff not found");
        }

        await _repository.ToggleStatusAsync(id);

        await _repository.SaveChangesAsync();
    }

    public async Task<StaffDto?> GetByIdAsync(Guid id)
    {
        var staff = await _repository
            .GetByIdAsync(id);

        if (staff == null)
            return null;

        return Map(staff);
    }

    public async Task<List<StaffDto>> GetAllAsync()
    {
        var staff = await _repository
            .GetAllAsync();

        return staff
            .Select(Map)
            .ToList();
    }

    public async Task<PagedResult<StaffDto>>
        SearchAsync(StaffQueryRequest request)
    {
        var result = await _repository
            .SearchAsync(request);

        return new PagedResult<StaffDto>
        {
            Items = result.Items
                .Select(Map)
                .ToList(),

            Page = result.Page,

            PageSize = result.PageSize,

            TotalCount = result.TotalCount,

            TotalPages = result.TotalPages
        };
    }
    public async Task<UserStatisticsDto>
        GetStatisticsAsync(
            UserStatisticsRequest request)
    {
        var result =
            await SearchAsync(
                new StaffQueryRequest
                {
                    ScopeNodeId =
                        request.ScopeNodeId,

                    Page = 1,

                    PageSize = int.MaxValue
                });

        return new UserStatisticsDto
        {
            TotalStaff =
                result.Items.Count,

            ActiveStaff =
                result.Items.Count(
                    x => x.IsActive),

            InactiveStaff =
                result.Items.Count(
                    x => !x.IsActive)
        };
    }

    private static StaffDto Map(Staff staff)
    {
        string facultyName =
            staff.StructureNode.Parent?.Name
            ?? string.Empty;

        return new StaffDto
        {
            Id = staff.Id,

            EmployeeCode = staff.EmployeeCode,

            Name = staff.Name,

            NationalId = staff.NationalId,

            BirthDate = staff.BirthDate,

            PhoneNumber = staff.PhoneNumber,

            Email = staff.Email,

            Role = staff.Role,

            JobTitle = staff.JobTitle,

            StructureNodeId = staff.StructureNodeId,

            StructureNodeName = staff.StructureNode.Name,

            FacultyName = facultyName,

            IsActive = staff.IsActive,

            PasswordExpiry = staff.PasswordExpiry,

            CreatedAt = staff.CreatedAt
        };
    }

    private async Task<string>
            GenerateEmployeeCodeAsync()
    {
        var lastCode =
            await _repository
                .GetLastEmployeeCodeAsync();

        if (string.IsNullOrWhiteSpace(
            lastCode))
        {
            return "EMP-1001";
        }

        var numberPart =
            lastCode.Replace(
                "EMP-",
                "");

        int number =
            int.Parse(numberPart);

        number++;

        return $"EMP-{number}";
    }
}
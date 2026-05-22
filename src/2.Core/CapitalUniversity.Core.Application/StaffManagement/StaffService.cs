using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.StaffManagement;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using System.Text.Json;

namespace CapitalUniversity.Core.Application.StaffManagement;

public class StaffService : IStaffService
{
    private readonly IStaffRepository _repository;
    private readonly IStructureNodeRepository _structureRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILocalizationService _localizationService;

    private readonly ISessionVersionService _sessionVersions;

    private readonly IUnitOfWork _unitOfWork;

    private readonly ILocalizationService _localization;

    public StaffService(
        IStaffRepository repository,
        IStructureNodeRepository structureRepository,
        ISessionVersionService sessionVersions,
        IUnitOfWork unitOfWork,
        ILocalizationService localization)
    {
        _repository = repository;
        _structureRepository = structureRepository;
        _sessionVersions = sessionVersions;
        _unitOfWork = unitOfWork;
        _localization = localization;
    }

    public async Task<Guid> CreateAsync(CreateStaffRequest request)
    {
        if (await _repository.EmailExistsAsync(request.Email))
        {
            throw new Exception("Email already exists");
        }

        if (await _repository.NationalIdExistsAsync(request.NationalId))
        {
            throw new Exception("National ID already exists");
        }

        if (request.Password != request.ConfirmPassword)
        {
            throw new Exception("Passwords do not match");
        }

        var hashedPassword = _passwordHasher.HashPassword(request.Password);

        var structureNode = await _structureRepository.GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new Exception("Structure node not found");
        }

        if (string.IsNullOrWhiteSpace(request.EmployeeCode))
        {
            request.EmployeeCode = await GenerateEmployeeCodeAsync();
        }

        bool codeExists = await _repository.EmployeeCodeExistsAsync(request.EmployeeCode);

        if (codeExists)
        {
            throw new Exception("Employee code already exists");
        }

        var nameJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "ar", request.NameAr },
            { "en", string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr : request.NameEn }
        });

        var staff = new Staff
        {
            Id = Guid.NewGuid(),

            EmployeeCode = request.EmployeeCode,

            PasswordHash = hashedPassword,

            Name = nameJson,

            NationalId = request.NationalId,

            BirthDate = request.BirthDate,

            PhoneNumber = request.PhoneNumber,

            Email = request.Email,

            Role = request.Role,

            JobTitle = request.JobTitle,

            StructureNodeId = request.StructureNodeId,

            PasswordExpiry = request.PasswordExpiry,

            IsActive = true
        };

        await _repository.AddAsync(staff);

        await _unitOfWork.SaveChangesAsync();

        // P2.6 — evict any negative-cached "user not found" entry so the new
        // staff's first authenticated request resolves cleanly.
        await _sessionVersions.InvalidateCacheAsync(staff.Id);

        return staff.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateStaffRequest request)
    {
        var staff = await _repository.GetByIdAsync(id);

        if (staff == null)
            throw new Exception("Staff not found");

        var structureNode = await _structureRepository.GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new Exception("Structure node not found");
        }

        bool emailExists = await _repository.EmailExistsAsync(request.Email);

        if (emailExists && staff.Email != request.Email)
        {
            throw new Exception("Email already exists");
        }

        bool nationalIdExists = await _repository.NationalIdExistsAsync(request.NationalId);

        if (nationalIdExists && staff.NationalId != request.NationalId)
        {
            throw new Exception("National ID already exists");
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password != request.ConfirmPassword)
            {
                throw new Exception("Passwords do not match");
            }
            staff.PasswordHash = _passwordHasher.HashPassword(request.Password);
        }

        if (!string.IsNullOrEmpty(request.NameAr) || !string.IsNullOrEmpty(request.NameEn))
        {
            var dict = new Dictionary<string, string>();
            try { dict = JsonSerializer.Deserialize<Dictionary<string, string>>(staff.Name) ?? new(); }
            catch { dict = new(); }
            if (!string.IsNullOrEmpty(request.NameAr)) dict["ar"] = request.NameAr;
            if (!string.IsNullOrEmpty(request.NameEn)) dict["en"] = request.NameEn;
            staff.Name = JsonSerializer.Serialize(dict);
        }

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

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        bool exists = await _repository.ExistsAsync(id);

        if (!exists)
            throw new Exception("Staff not found");

        await _repository.SoftDeleteAsync(id);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ToggleStatusAsync(Guid id)
    {
        bool exists = await _repository.ExistsAsync(id);

        if (!exists)
        {
            throw new Exception("Staff not found");
        }

        await _repository.ToggleStatusAsync(id);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<StaffDto?> GetByIdAsync(Guid id)
    {
        var staff = await _repository.GetByIdAsync(id);

        if (staff == null)
            return null;

        return MapInstance(staff);
    }

    public async Task<List<StaffDto>> GetAllAsync()
    {
        var staff = await _repository.GetAllAsync();

        return staff
            .Select(MapInstance)
            .ToList();
    }

    public async Task<PagedResult<StaffDto>> SearchAsync(StaffQueryRequest request)
    {
        var result = await _repository.SearchAsync(request);

        return new PagedResult<StaffDto>
        {
            Items = result.Items
                .Select(MapInstance)
                .ToList(),

            Page = result.Page,

            PageSize = result.PageSize,

            TotalCount = result.TotalCount,

            TotalPages = result.TotalPages
        };
    }

    public async Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request)
    {
        var result = await SearchAsync(new StaffQueryRequest
        {
            ScopeNodeId = request.ScopeNodeId,

            Page = 1,

            PageSize = int.MaxValue
        });

        return new UserStatisticsDto
        {
            TotalStaff = result.Items.Count,
            ActiveStaff = result.Items.Count(x => x.IsActive),
            InactiveStaff = result.Items.Count(x => !x.IsActive)
        };
    }

    /// <summary>
    /// Project staff onto its DTO, decoding every bilingual string against
    /// the current culture. Personal <c>Name</c> is also decoded — operators
    /// store the canonical <c>{"ar":"منة مجدى","en":"Menna Magdy"}</c> JSON
    /// for bilingual records and plain text for single-language data; the
    /// resolver round-trips both shapes safely.
    /// </summary>
    private StaffDto MapInstance(Staff staff)
    {
        string facultyName =
            staff.StructureNode.Parent?.Name is { } parentName
                ? _localization.Get<string>(parentName)
                : string.Empty;

        return new StaffDto
        {
            Id = staff.Id,

            EmployeeCode = staff.EmployeeCode,

            Name = _localization.Get<string>(staff.Name),

            LocalizedName = localizedName,

            NationalId = staff.NationalId,

            BirthDate = staff.BirthDate,

            PhoneNumber = staff.PhoneNumber,

            Email = staff.Email,

            Role = staff.Role,

            JobTitle = _localization.Get<string>(staff.JobTitle),

            StructureNodeId = staff.StructureNodeId,

            StructureNodeName = _localization.Get<string>(staff.StructureNode.Name),

            FacultyName = facultyName,

            IsActive = staff.IsActive,

            PasswordExpiry = staff.PasswordExpiry,

            CreatedAt = staff.CreatedAt
        };
    }

    private async Task<string> GenerateEmployeeCodeAsync()
    {
        var lastCode = await _repository.GetLastEmployeeCodeAsync();

        if (string.IsNullOrWhiteSpace(lastCode))
        {
            return "EMP-1001";
        }

        var numberPart = lastCode.Replace("EMP-","");

        int number = int.Parse(numberPart);

        number++;

        return $"EMP-{number}";
    }
}
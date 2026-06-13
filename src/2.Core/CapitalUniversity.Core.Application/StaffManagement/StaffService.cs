using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Core.Abstractions.StaffManagement;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Domain.Common.Exceptions;
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

    public StaffService(
        IStaffRepository repository,
        IStructureNodeRepository structureRepository,
        IPasswordHasher passwordHasher,
        ILocalizationService localizationService,
        ISessionVersionService sessionVersions,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _structureRepository = structureRepository;
        _passwordHasher = passwordHasher;
        _localizationService = localizationService;
        _sessionVersions = sessionVersions;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> CreateAsync(CreateStaffRequest request)
    {
        if (await _repository.EmailExistsAsync(request.Email))
        {
            throw new ValidationException("Email", LocalizedKeys.StaffManagement.EmailInUse);
        }

        if (await _repository.NationalIdExistsAsync(request.NationalId))
        {
            throw new ValidationException("NationalId", LocalizedKeys.StaffManagement.NationalIdInUse);
        }

        if (request.Password != request.ConfirmPassword)
        {
            throw new ValidationException("Password", LocalizedKeys.StaffManagement.PasswordsDoNotMatch);
        }

        var hashedPassword = _passwordHasher.HashPassword(request.Password);

        var structureNode = await _structureRepository.GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new ValidationException("StructureNodeId", LocalizedKeys.StaffManagement.StructureNodeNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.EmployeeCode))
        {
            request.EmployeeCode = await GenerateEmployeeCodeAsync();
        }

        bool codeExists = await _repository.EmployeeCodeExistsAsync(request.EmployeeCode);

        if (codeExists)
        {
            throw new ValidationException("EmployeeCode", LocalizedKeys.StaffManagement.CodeInUse);
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

            PhotoUrl = request.PhotoUrl,

            Gender = request.Gender,

            Qualification = request.Qualification,

            StructureNodeId = request.StructureNodeId,

            PasswordExpiry = request.PasswordExpiry,

            IsActive = true
        };

        await _repository.AddAsync(staff);
        await _repository.SaveChangesAsync();

        // P2.6 — evict any negative-cached "user not found" entry so the new
        // staff's first authenticated request resolves cleanly.
        await _sessionVersions.InvalidateCacheAsync(staff.Id);

        return staff.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateStaffRequest request)
    {
        var staff = await _repository.GetByIdAsync(id);

        if (staff == null)
            throw new NotFoundException(LocalizedKeys.StaffManagement.StaffNotFound);

        var structureNode = await _structureRepository.GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new ValidationException("StructureNodeId", LocalizedKeys.StaffManagement.StructureNodeNotFound);
        }

        bool emailExists = await _repository.EmailExistsAsync(request.Email);

        if (emailExists && staff.Email != request.Email)
        {
            throw new ValidationException("Email", LocalizedKeys.StaffManagement.EmailInUse);
        }

        bool nationalIdExists = await _repository.NationalIdExistsAsync(request.NationalId);

        if (nationalIdExists && staff.NationalId != request.NationalId)
        {
            throw new ValidationException("NationalId", LocalizedKeys.StaffManagement.NationalIdInUse);
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password != request.ConfirmPassword)
            {
                throw new ValidationException("Password", LocalizedKeys.StaffManagement.PasswordsDoNotMatch);
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

        staff.PhotoUrl = request.PhotoUrl ?? staff.PhotoUrl;

        staff.Gender = request.Gender ?? staff.Gender;

        staff.Qualification = request.Qualification ?? staff.Qualification;

        staff.StructureNodeId = request.StructureNodeId;

        staff.IsActive = request.IsActive;

        staff.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(staff);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        bool exists = await _repository.ExistsAsync(id);

        if (!exists)
            throw new NotFoundException(LocalizedKeys.StaffManagement.StaffNotFound);

        await _repository.SoftDeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    //---
    public async Task<BulkActionResult> SetStatusManyAsync(IReadOnlyList<Guid> ids, bool isActive, CancellationToken cancellationToken = default)
    {
        var succeeded = new List<Guid>(ids.Count);
        var failures = new List<BulkActionFailure>();

        foreach (var id in ids.Distinct())
        {
            try
            {
                var staff = await _repository.GetByIdAsync(id);
                if (staff is null)
                {
                    failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.NotFound, Message = LocalizedKeys.StaffManagement.StaffNotFound });
                    continue;
                }
                if (staff.IsActive == isActive)
                {
                    succeeded.Add(id);
                    continue;
                }
                staff.IsActive = isActive;
                await _repository.UpdateAsync(staff);
                succeeded.Add(id);
            }
            catch (Exception ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.Unknown, Message = ex.Message });
            }
        }

        if (succeeded.Count > 0) await _unitOfWork.SaveChangesAsync();
        return BulkActionResult.From(succeeded, failures);
    }

    public async Task<BulkActionResult> DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var succeeded = new List<Guid>(ids.Count);
        var failures = new List<BulkActionFailure>();

        foreach (var id in ids.Distinct())
        {
            try
            {
                if (!await _repository.ExistsAsync(id))
                {
                    failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.NotFound, Message = LocalizedKeys.StaffManagement.StaffNotFound });
                    continue;
                }
                await _repository.SoftDeleteAsync(id);
                succeeded.Add(id);
            }
            catch (Exception ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.Unknown, Message = ex.Message });
            }
        }

        if (succeeded.Count > 0) await _unitOfWork.SaveChangesAsync();
        return BulkActionResult.From(succeeded, failures);
    }
    //---

    public async Task ToggleStatusAsync(Guid id)
    {
        bool exists = await _repository.ExistsAsync(id);

        if (!exists)
        {
            throw new NotFoundException(LocalizedKeys.StaffManagement.StaffNotFound);
        }

        await _repository.ToggleStatusAsync(id);
        await _repository.SaveChangesAsync();
    }

    public async Task<StaffDto?> GetByIdAsync(Guid id)
    {
        var staff = await _repository.GetByIdAsync(id);

        if (staff == null)
            return null;

        return Map(staff);
    }

    public async Task<List<StaffDto>> GetAllAsync()
    {
        var staff = await _repository.GetAllAsync();

        return staff.Select(Map).ToList();
    }

    public async Task<PagedResult<StaffDto>> SearchAsync(StaffQueryRequest request)
    {
        var result = await _repository.SearchAsync(request);

        return new PagedResult<StaffDto>
        {
            Items = result.Items.Select(Map).ToList(),

            Page = result.Page,

            PageSize = result.PageSize,

            TotalCount = result.TotalCount,

            TotalPages = result.TotalPages
        };
    }

    public async Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request)
    {
        return await _repository.GetStatisticsAsync(request);
    }

    public async Task UpdatePhotoAsync(Guid id, string photoUrl)
    {
        var staff = await _repository.GetByIdAsync(id);
        if (staff == null)
            throw new NotFoundException(LocalizedKeys.StaffManagement.StaffNotFound);

        staff.PhotoUrl = photoUrl;
        staff.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(staff);
        await _unitOfWork.SaveChangesAsync();
    }

    private StaffDto Map(Staff staff)
    {
        var localizedName = _localizationService.GetLocalizedString(staff.Name);

        string structureNodeName = staff.StructureNode != null
            ? _localizationService.GetLocalizedString(staff.StructureNode.Name)
            : string.Empty;

        string facultyName = string.Empty;
        var currentNode = staff.StructureNode;

        while (currentNode != null)
        {
            if (currentNode.Type == StructureNodeType.Faculty)
            {
                facultyName = _localizationService.GetLocalizedString(currentNode.Name);
                break;
            }
            currentNode = currentNode.Parent;
        }

        return new StaffDto
        {
            Id = staff.Id,

            EmployeeCode = staff.EmployeeCode,

            Name = staff.Name,

            LocalizedName = localizedName,

            NationalId = staff.NationalId,

            BirthDate = staff.BirthDate,

            PhoneNumber = staff.PhoneNumber,

            Email = staff.Email,

            Role = staff.Role,

            JobTitle = _localizationService.GetLocalizedString(staff.JobTitle),

            PhotoUrl = staff.PhotoUrl,

            Gender = staff.Gender,

            Qualification = staff.Qualification,

            StructureNodeId = staff.StructureNodeId,

            StructureNodeName = structureNodeName,

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

        var numberPart = lastCode.Replace("EMP-", "");

        int number = int.Parse(numberPart);

        number++;

        return $"EMP-{number}";
    }
}
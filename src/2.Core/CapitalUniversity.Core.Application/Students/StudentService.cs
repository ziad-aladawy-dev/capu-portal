using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Students;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using System.Text.Json;

namespace CapitalUniversity.Core.Application.Students;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _repository;
    private readonly IStructureNodeRepository _structureRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILocalizationService _localizationService;

    public StudentService(
        IStudentRepository repository,
        IStructureNodeRepository structureRepository,
        IPasswordHasher passwordHasher,
        ILocalizationService localizationService)
    {
        _repository = repository;
        _structureRepository = structureRepository;
        _passwordHasher = passwordHasher;
        _localizationService = localizationService;
    }

    public async Task<Guid> CreateAsync(CreateStudentRequest request)
    {
        bool codeExists = await _repository.StudentCodeExistsAsync(request.StudentCode);

        if (codeExists)
        {
            throw new ValidationException("StudentCode", LocalizedKeys.StudentInformation.CodeInUse);
        }

        if (await _repository.EmailExistsAsync(request.Email))
        {
            throw new ValidationException("Email", LocalizedKeys.StudentInformation.EmailInUse);
        }

        if (await _repository.NationalIdExistsAsync(request.NationalId))
        {
            throw new ValidationException("NationalId", LocalizedKeys.StudentInformation.NationalIdInUse);
        }

        if (request.Password != request.ConfirmPassword)
        {
            throw new ValidationException("Password", LocalizedKeys.StudentInformation.PasswordsDoNotMatch);
        }

        var hashedPassword = _passwordHasher.HashPassword(request.Password);

        var structureNode = await _structureRepository.GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new ValidationException("StructureNodeId", LocalizedKeys.StudentInformation.StructureNodeNotFound);
        }

        if (structureNode.Type != StructureNodeType.Level)
        {
            throw new ValidationException("StructureNodeId", LocalizedKeys.StudentInformation.MustBeLevelNode);
        }

        if (string.IsNullOrWhiteSpace(request.StudentCode))
        {
            request.StudentCode = await GenerateStudentCodeAsync();
        }

        var nameJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "ar", request.NameAr },
            { "en", string.IsNullOrWhiteSpace(request.NameEn) ? request.NameAr : request.NameEn }
        });

        var student = new Student
        {
            Id = Guid.NewGuid(),

            StudentCode = request.StudentCode,

            Name = nameJson,

            NationalId = request.NationalId,

            BirthDate = request.BirthDate,

            PhoneNumber = request.PhoneNumber,

            Email = request.Email,

            PhotoUrl = request.PhotoUrl,

            Gender = request.Gender,

            GuardianName = request.GuardianName,

            GuardianPhone = request.GuardianPhone,

            StructureNodeId = request.StructureNodeId,

            PasswordHash = hashedPassword,

            PasswordExpiry = request.PasswordExpiry,

            IsActive = true
        };

        await _repository.AddAsync(student);
        await _repository.SaveChangesAsync();

        // P2.6 — evict any negative-cached "user not found" entry so the new
        // student's first authenticated request resolves cleanly.

        return student.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateStudentRequest request)
    {
        var student = await _repository.GetByIdAsync(id);

        if (student == null)
            throw new NotFoundException(LocalizedKeys.StudentInformation.StudentNotFound);

        if (request.StructureNodeId.HasValue)
        {
            var structureNode = await _structureRepository.GetByIdAsync(request.StructureNodeId.Value);

            if (structureNode == null)
            {
                throw new ValidationException("StructureNodeId", LocalizedKeys.StudentInformation.StructureNodeNotFound);
            }

            if (structureNode.Type != StructureNodeType.Level)
            {
                throw new ValidationException("StructureNodeId", LocalizedKeys.StudentInformation.MustBeLevelNode);
            }

            student.StructureNodeId = request.StructureNodeId.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && student.Email != request.Email)
        {
            if (await _repository.EmailExistsAsync(request.Email))
            {
                throw new ValidationException("Email", LocalizedKeys.StudentInformation.EmailInUse);
            }

            student.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.NationalId) && student.NationalId != request.NationalId)
        {
            if (await _repository.NationalIdExistsAsync(request.NationalId))
            {
                throw new ValidationException("NationalId", LocalizedKeys.StudentInformation.NationalIdInUse);
            }

            student.NationalId = request.NationalId;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password != request.ConfirmPassword)
            {
                throw new ValidationException("Password", LocalizedKeys.StudentInformation.PasswordsDoNotMatch);
            }
            student.PasswordHash = _passwordHasher.HashPassword(request.Password);
        }

        if (!string.IsNullOrEmpty(request.NameAr) || !string.IsNullOrEmpty(request.NameEn))
        {
            var dict = new Dictionary<string, string>();
            try { dict = JsonSerializer.Deserialize<Dictionary<string, string>>(student.Name) ?? new(); }
            catch { dict = new(); }
            if (!string.IsNullOrEmpty(request.NameAr)) dict["ar"] = request.NameAr;
            if (!string.IsNullOrEmpty(request.NameEn)) dict["en"] = request.NameEn;
            student.Name = JsonSerializer.Serialize(dict);
        }

        student.BirthDate = request.BirthDate ?? student.BirthDate;

        // Empty string clears the phone number; null leaves it unchanged.
        student.PhoneNumber = request.PhoneNumber ?? student.PhoneNumber;

        student.PhotoUrl = request.PhotoUrl ?? student.PhotoUrl;

        student.Gender = request.Gender ?? student.Gender;

        student.GuardianName = request.GuardianName ?? student.GuardianName;

        student.GuardianPhone = request.GuardianPhone ?? student.GuardianPhone;

        student.IsActive = request.IsActive ?? student.IsActive;

        student.PasswordExpiry = request.PasswordExpiry ?? student.PasswordExpiry;

        student.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(student);
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        bool exists = await _repository.ExistsAsync(id);

        if (!exists)
            throw new NotFoundException(LocalizedKeys.StudentInformation.StudentNotFound);

        await _repository.SoftDeleteAsync(id);
        await _repository.SaveChangesAsync();
    }

    public async Task ToggleStatusAsync(Guid id)
    {
        bool exists = await _repository.ExistsAsync(id);

        if (!exists)
        {
            throw new NotFoundException(LocalizedKeys.StudentInformation.StudentNotFound);
        }

        await _repository.ToggleStatusAsync(id);
        await _repository.SaveChangesAsync();
    }

    public async Task<StudentDto?> GetByIdAsync(Guid id)
    {
        var student = await _repository.GetByIdAsync(id);

        if (student == null)
            return null;

        return Map(student);
    }

    public async Task<List<StudentDto>> GetAllAsync()
    {
        var students = await _repository.GetAllAsync();

        return students.Select(Map).ToList();
    }

    public async Task<PagedResult<StudentDto>> SearchAsync(StudentQueryRequest request)
    {
        var result = await _repository.SearchAsync(request);

        return new PagedResult<StudentDto>
        {
            Items = result.Items.Select(Map).ToList(),

            Page = result.Page,

            PageSize = result.PageSize,

            TotalCount = result.TotalCount,

            TotalPages = result.TotalPages
        };
    }

    public async Task UpdatePhotoAsync(Guid id, string photoUrl)
    {
        var student = await _repository.GetByIdAsync(id);
        if (student == null)
            throw new NotFoundException(LocalizedKeys.StudentInformation.StudentNotFound);

        student.PhotoUrl = photoUrl;
        student.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(student);
        await _repository.SaveChangesAsync();
    }

    public async Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request)
    {
        var result = await SearchAsync(new StudentQueryRequest
        {
            ScopeNodeId = request.ScopeNodeId,

            Page = 1,

            PageSize = int.MaxValue
        });

        return new UserStatisticsDto
        {
            TotalStudents = result.Items.Count,
            ActiveStudents = result.Items.Count(x => x.IsActive),
            InactiveStudents = result.Items.Count(x => !x.IsActive)
        };
    }

    private StudentDto Map(Student student)
    {
        var localizedName = _localizationService.GetLocalizedString(student.Name);

        var levelNode = student.StructureNode;

        string facultyName = string.Empty;

        string programName = string.Empty;

        string levelName = string.Empty;

        if (levelNode != null)
        {
            levelName = _localizationService.GetLocalizedString(levelNode.Name);
            var currentNode = levelNode.Parent;
            while (currentNode != null)
            {
                if (currentNode.Type == StructureNodeType.Program && string.IsNullOrEmpty(programName))
                    programName = _localizationService.GetLocalizedString(currentNode.Name);
                else if (currentNode.Type == StructureNodeType.Faculty && string.IsNullOrEmpty(facultyName))
                    facultyName = _localizationService.GetLocalizedString(currentNode.Name);
                currentNode = currentNode.Parent;
            }
        }

        return new StudentDto
        {
            Id = student.Id,

            StudentCode = student.StudentCode,

            Name = student.Name,

            LocalizedName = localizedName,

            NationalId = student.NationalId,

            BirthDate = student.BirthDate,

            PhoneNumber = student.PhoneNumber,

            Email = student.Email,

            PhotoUrl = student.PhotoUrl,

            Gender = student.Gender,

            GuardianName = student.GuardianName,

            GuardianPhone = student.GuardianPhone,

            StructureNodeId = student.StructureNodeId,

            StructureNodeName = levelName,

            FacultyName = facultyName,

            ProgramName = programName,

            LevelName = levelName,

            IsActive = student.IsActive,

            PasswordExpiry = student.PasswordExpiry,

            CreatedAt = student.CreatedAt
        };
    }

    private async Task<string> GenerateStudentCodeAsync()
    {
        var lastCode = await _repository.GetLastStudentCodeAsync();

        if (string.IsNullOrWhiteSpace(lastCode))
        {
            return "STU-1001";
        }

        var numberPart = lastCode.Replace("STU-", "");

        int number = int.Parse(numberPart);

        number++;

        return $"STU-{number}";
    }
}
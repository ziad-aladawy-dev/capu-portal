using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Students;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.Application.Students;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _repository;

    private readonly IStructureNodeRepository _structureRepository;

    private readonly IPasswordHasher _passwordHasher;

    private readonly ISessionVersionService _sessionVersions;

    private readonly IUnitOfWork _unitOfWork;

    private readonly ILocalizationService _localization;

    public StudentService(
        IStudentRepository repository,
        IStructureNodeRepository structureRepository,
        IPasswordHasher passwordHasher,
        ISessionVersionService sessionVersions,
        IUnitOfWork unitOfWork,
        ILocalizationService localization)
    {
        _repository = repository;
        _structureRepository = structureRepository;
        _passwordHasher = passwordHasher;
        _sessionVersions = sessionVersions;
        _unitOfWork = unitOfWork;
        _localization = localization;
    }

    public async Task<Guid> CreateAsync(CreateStudentRequest request)
    {
        bool codeExists = await _repository
            .StudentCodeExistsAsync(
                request.StudentCode);

        if (codeExists)
        {
            throw new Exception(
                "Student code already exists");
        }

        if (await _repository.EmailExistsAsync(
            request.Email))
        {
            throw new Exception(
                "Email already exists");
        }

        if (await _repository.NationalIdExistsAsync(
            request.NationalId))
        {
            throw new Exception(
                "National ID already exists");
        }

        if (request.Password != request.ConfirmPassword)
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

        if (structureNode.Type != StructureNodeType.Level)
        {
            throw new Exception(
                "Student must be assigned to level node");
        }

        if (string.IsNullOrWhiteSpace(
            request.StudentCode))
        {
            request.StudentCode =
                await GenerateStudentCodeAsync();
        }

        var student = new Student
        {
            Id = Guid.NewGuid(),

            StudentCode = request.StudentCode,

            Name = request.Name,

            NationalId = request.NationalId,

            BirthDate = request.BirthDate,

            PhoneNumber = request.PhoneNumber,

            Email = request.Email,

            StructureNodeId = request.StructureNodeId,

            PasswordHash = hashedPassword,

            PasswordExpiry = request.PasswordExpiry,

            IsActive = true
        };

        await _repository.AddAsync(student);

        await _unitOfWork.SaveChangesAsync();

        // P2.6 — evict any negative-cached "user not found" entry so the new
        // student's first authenticated request resolves cleanly.
        await _sessionVersions.InvalidateCacheAsync(student.Id);

        return student.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateStudentRequest request)
    {
        var student = await _repository
            .GetByIdAsync(id);

        if (student == null)
            throw new Exception("Student not found");

        var structureNode = await _structureRepository
            .GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new Exception(
                "Structure node not found");
        }

        if (structureNode.Type != StructureNodeType.Level)
        {
            throw new Exception(
                "Student must be assigned to a level");
        }

        bool emailExists =
            await _repository.EmailExistsAsync(
                request.Email);

        if (emailExists &&
            student.Email != request.Email)
        {
            throw new Exception(
                "Email already exists");
        }

        bool nationalIdExists =
            await _repository
                .NationalIdExistsAsync(
                    request.NationalId);

        if (nationalIdExists &&
            student.NationalId !=
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

            student.PasswordHash = _passwordHasher.HashPassword(request.Password);
        }

        student.Name = request.Name;

        student.NationalId = request.NationalId;

        student.BirthDate = request.BirthDate;

        student.PhoneNumber = request.PhoneNumber;

        student.Email = request.Email;

        student.StructureNodeId =
            request.StructureNodeId;

        student.IsActive = request.IsActive;

        student.PasswordExpiry = request.PasswordExpiry;

        student.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(student);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        bool exists = await _repository
            .ExistsAsync(id);

        if (!exists)
            throw new Exception("Student not found");

        await _repository.SoftDeleteAsync(id);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ToggleStatusAsync(Guid id)
    {
        bool exists = await _repository
            .ExistsAsync(id);

        if (!exists)
        {
            throw new Exception(
                "Student not found");
        }

        await _repository.ToggleStatusAsync(id);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<StudentDto?> GetByIdAsync(Guid id)
    {
        var student = await _repository
            .GetByIdAsync(id);

        if (student == null)
            return null;

        return MapInstance(student);
    }

    public async Task<List<StudentDto>> GetAllAsync()
    {
        var students = await _repository
            .GetAllAsync();

        return students
            .Select(MapInstance)
            .ToList();
    }

    public async Task<PagedResult<StudentDto>>
        SearchAsync(StudentQueryRequest request)
    {
        var result = await _repository
            .SearchAsync(request);

        return new PagedResult<StudentDto>
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

    public async Task<UserStatisticsDto>
        GetStatisticsAsync(
            UserStatisticsRequest request)
    {
        var result =
            await SearchAsync(
                new StudentQueryRequest
                {
                    ScopeNodeId =
                        request.ScopeNodeId,

                    Page = 1,

                    PageSize = int.MaxValue
                });

        return new UserStatisticsDto
        {
            TotalStudents =
                result.Items.Count,

            ActiveStudents =
                result.Items.Count(
                    x => x.IsActive),

            InactiveStudents =
                result.Items.Count(
                    x => !x.IsActive)
        };
    }

    /// <summary>
    /// Project a <see cref="Student"/> onto its DTO, decoding every bilingual
    /// string against the caller's culture. Personal <c>Name</c> is also
    /// decoded — operators store the canonical
    /// <c>{"ar":"منة مجدى","en":"Menna Magdy"}</c> JSON for bilingual records
    /// and plain text for single-language data; the resolver round-trips both.
    /// </summary>
    private StudentDto MapInstance(
            Student student)
    {
        var levelNode = student.StructureNode;

        string facultyName = string.Empty;

        string programName = string.Empty;

        string levelName =
            _localization.Get<string>(student.StructureNode.Name);

        var currentNode = levelNode?.Parent;

        while (currentNode != null)
        {
            if (currentNode.Type == StructureNodeType.Program)
            {
                programName =
                    _localization.Get<string>(currentNode.Name);
                break;
            }
            currentNode = currentNode.Parent;
        }

        currentNode = levelNode?.Parent;
        while (currentNode != null)
        {
            if (currentNode.Type == StructureNodeType.Faculty)
            {
                facultyName =
                    _localization.Get<string>(currentNode.Name);
                break;
            }
            currentNode = currentNode.Parent;
        }

        //var programNode =
        //    student.StructureNode.Parent;

        //if (programNode != null)
        //{
        //    programName =
        //        programNode.Name;
        //}

        //var facultyNode =
        //    programNode?.Parent;

        //if (facultyNode != null)
        //{
        //    facultyName =
        //        facultyNode.Name;
        //}

        return new StudentDto
        {
            Id = student.Id,

            StudentCode =
                student.StudentCode,

            Name = _localization.Get<string>(student.Name),

            NationalId =
                student.NationalId,

            BirthDate =
                student.BirthDate,

            PhoneNumber =
                student.PhoneNumber,

            Email = student.Email,

            StructureNodeId =
                student.StructureNodeId,

            StructureNodeName =
                levelName,

            FacultyName =
                facultyName,

            ProgramName =
                programName,

            LevelName =
                levelName,

            IsActive =
                student.IsActive,

            PasswordExpiry =
                student.PasswordExpiry,

            CreatedAt =
                student.CreatedAt
        };
    }

    private async Task<string>
        GenerateStudentCodeAsync()
    {
        var lastCode = await _repository
            .GetLastStudentCodeAsync();

        if (string.IsNullOrWhiteSpace(
            lastCode))
        {
            return "STU-1001";
        }

        var numberPart = lastCode
            .Replace("STU-", "");

        int number =
            int.Parse(numberPart);

        number++;

        return $"STU-{number}";
    }
}
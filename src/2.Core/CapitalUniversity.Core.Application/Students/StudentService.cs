using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Students;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Repositories;

namespace CapitalUniversity.Core.Application.Students;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _repository;

    private readonly IStructureNodeRepository
        _structureRepository;

    public StudentService(
        IStudentRepository repository,
        IStructureNodeRepository structureRepository)
    {
        _repository = repository;
        _structureRepository = structureRepository;
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

        var structureNode = await _structureRepository
            .GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new Exception(
                "Structure node not found");
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

            PasswordHash = "NOT_SET",

            IsActive = true
        };

        await _repository.AddAsync(student);

        await _repository.SaveChangesAsync();

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

        student.Name = request.Name;

        student.NationalId = request.NationalId;

        student.BirthDate = request.BirthDate;

        student.PhoneNumber = request.PhoneNumber;

        student.Email = request.Email;

        student.StructureNodeId =
            request.StructureNodeId;

        student.IsActive = request.IsActive;

        student.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(student);

        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        bool exists = await _repository
            .ExistsAsync(id);

        if (!exists)
            throw new Exception("Student not found");

        await _repository.SoftDeleteAsync(id);

        await _repository.SaveChangesAsync();
    }

    public async Task<StudentDto?> GetByIdAsync(Guid id)
    {
        var student = await _repository
            .GetByIdAsync(id);

        if (student == null)
            return null;

        return Map(student);
    }

    public async Task<List<StudentDto>> GetAllAsync()
    {
        var students = await _repository
            .GetAllAsync();

        return students
            .Select(Map)
            .ToList();
    }

    private static StudentDto Map(Student student)
    {
        return new StudentDto
        {
            Id = student.Id,

            StudentCode = student.StudentCode,

            Name = student.Name,

            NationalId = student.NationalId,

            BirthDate = student.BirthDate,

            PhoneNumber = student.PhoneNumber,

            Email = student.Email,

            StructureNodeId = student.StructureNodeId,

            StructureNodeName = student.StructureNode.Name,

            IsActive = student.IsActive
        };
    }
}
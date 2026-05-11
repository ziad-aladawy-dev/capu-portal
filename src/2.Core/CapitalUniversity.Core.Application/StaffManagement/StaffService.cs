using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.StaffManagement;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Application.StaffManagement;

public class StaffService : IStaffService
{
    private readonly IStaffRepository _repository;

    private readonly IStructureNodeRepository
        _structureRepository;

    public StaffService(
        IStaffRepository repository,
        IStructureNodeRepository structureRepository)
    {
        _repository = repository;
        _structureRepository = structureRepository;
    }

    public async Task<Guid> CreateAsync(CreateStaffRequest request)
    {
        bool codeExists = await _repository
            .EmployeeCodeExistsAsync(
                request.EmployeeCode);

        if (codeExists)
        {
            throw new Exception(
                "Employee code already exists");
        }

        var structureNode = await _structureRepository
            .GetByIdAsync(request.StructureNodeId);

        if (structureNode == null)
        {
            throw new Exception(
                "Structure node not found");
        }

        var staff = new Staff
        {
            Id = Guid.NewGuid(),

            EmployeeCode = request.EmployeeCode,

            Name = request.Name,

            NationalId = request.NationalId,

            BirthDate = request.BirthDate,

            PhoneNumber = request.PhoneNumber,

            Email = request.Email,

            Role = request.Role,

            JobTitle = request.JobTitle,

            StructureNodeId =
                request.StructureNodeId,

            PasswordHash = "NOT_SET",

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

    private static StaffDto Map(Staff staff)
    {
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

            IsActive = staff.IsActive
        };
    }
}
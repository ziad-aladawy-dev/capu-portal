using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;

namespace CapitalUniversity.Core.Abstractions.UniversityStructure;

public interface IUniversityStructureService
{
    Task<List<StructureNodeDto>> GetTreeAsync();

    Task<StructureNodeDto?> GetByIdAsync(Guid id);

    Task<Guid> CreateNodeAsync(CreateStructureNodeRequest request);

    Task UpdateNodeAsync(Guid id, UpdateStructureNodeRequest request);

    Task DeleteNodeAsync(Guid id);
}
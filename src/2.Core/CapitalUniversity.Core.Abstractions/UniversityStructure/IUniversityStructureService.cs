using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;

namespace CapitalUniversity.Core.Abstractions.UniversityStructure;

public interface IUniversityStructureService
{
    Task<List<StructureNodeDto>> GetTreeAsync();

    Task<StructureNodeDto?> GetByIdAsync(Guid id);

    Task<Guid> CreateNodeAsync(CreateStructureNodeRequest request);

    Task UpdateNodeAsync(Guid id, UpdateStructureNodeRequest request);

    Task DeleteNodeAsync(Guid id);

    Task MoveNodeAsync(Guid nodeId, MoveStructureNodeRequest request);

    Task<List<StructureNodeDto>> GetRootsAsync();

    Task<List<StructureNodeDto>> GetChildrenAsync(Guid parentId);

    Task<List<BreadcrumbItemDto>> GetBreadcrumbAsync(Guid nodeId);

    Task<StructureNodeDto?> GetSubTreeAsync(Guid nodeId);

    Task<List<StructureNodeDto>> GetAncestorsChainAsync(Guid nodeId);

    Task ReorderNodeAsync(Guid nodeId, ReorderNodeRequest request);
}
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IStructureNodeRepository
{
    Task<StructureNode?> GetByIdAsync(Guid id);

    Task<List<StructureNode>> GetRootsAsync();

    Task<List<StructureNode>> GetChildrenAsync(Guid parentId);

    Task<List<StructureNode>> GetAllAsync();

    Task AddAsync(StructureNode node);

    Task UpdateAsync(StructureNode node);

    Task SoftDeleteAsync(Guid id);

    Task RecursiveSoftDeleteAsync(string path);

    Task<bool> ExistsAsync(Guid id);

    Task<List<StructureNode>> GetDescendantsAsync(string path);

    Task UpdateRangeAsync(List<StructureNode> nodes);

    Task<List<StructureNode>> GetChildrenOnlyAsync(Guid parentId);

    Task<List<StructureNode>> GetByIdsAsync(List<Guid> ids);

    Task<List<StructureNode>> GetDescendantsTreeAsync(string path);

    Task<List<StructureNode>> GetAncestorsAsync(List<Guid> ids);

    Task<List<StructureNode>> GetSiblingsAsync(Guid? parentId);
    Task SaveChangesAsync();
}
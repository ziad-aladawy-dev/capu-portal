using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Repositories;

public interface IStructureNodeRepository
{
    Task<StructureNode?> GetByIdAsync(Guid id);

    Task<List<StructureNode>> GetRootsAsync();

    Task<List<StructureNode>> GetChildrenAsync(Guid parentId);

    Task<List<StructureNode>> GetAllAsync();

    Task AddAsync(StructureNode node);

    Task UpdateAsync(StructureNode node);

    Task SoftDeleteAsync(Guid id);

    Task<bool> ExistsAsync(Guid id);

    Task SaveChangesAsync();
}
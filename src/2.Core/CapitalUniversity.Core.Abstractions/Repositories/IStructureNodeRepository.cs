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

    /// <summary>
    /// Rewrites <c>StructureNodePath</c> snapshots on every
    /// <c>StaffRoleAssignment</c> and <c>StaffPermissionOverride</c> whose stored
    /// path starts with <paramref name="oldPath"/>, replacing that prefix with
    /// <paramref name="newPath"/>. Called after a structure-node move so
    /// assignment-scoped permissions don't silently grant access to the old
    /// subtree or lose access to the new one. Returns the row count modified.
    /// </summary>
    Task<int> RepairPermissionPathPrefixAsync(string oldPath, string newPath, CancellationToken cancellationToken = default);
}

using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class StructureNodeRepository : IStructureNodeRepository
{
    private readonly CoreDbContext _context;

    public StructureNodeRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<StructureNode?> GetByIdAsync(Guid id)
    {
        return await _context.StructureNodes
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<StructureNode>> GetRootsAsync()
    {
        return await _context.StructureNodes
            .Where(x => x.ParentId == null)
            .OrderBy(x => x.Order)
            .ToListAsync();
    }

    public async Task<List<StructureNode>> GetChildrenAsync(Guid parentId)
    {
        return await _context.StructureNodes
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Order)
            .ToListAsync();
    }

    public async Task<List<StructureNode>> GetAllAsync()
    {
        return await _context.StructureNodes
            .OrderBy(x => x.Order)
            .ToListAsync();
    }

    public async Task AddAsync(StructureNode node)
    {
        await _context.StructureNodes.AddAsync(node);
    }

    public Task UpdateAsync(StructureNode node)
    {
        node.UpdatedAt = DateTime.UtcNow;

        _context.StructureNodes.Update(node);

        return Task.CompletedTask;
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        var node = await _context.StructureNodes
            .FirstOrDefaultAsync(x => x.Id == id);

        if (node == null)
            return;

        node.IsDeleted = true;
        node.UpdatedAt = DateTime.UtcNow;

        _context.StructureNodes.Update(node);
    }

    public async Task RecursiveSoftDeleteAsync(string path)
    {
        var nodes = await _context.StructureNodes
            .IgnoreQueryFilters()
            .Where(x =>
                x.Path.StartsWith(path) &&
                !x.IsDeleted)
            .ToListAsync();

        foreach (var node in nodes)
        {
            node.IsDeleted = true;
            node.UpdatedAt = DateTime.UtcNow;
        }

        _context.StructureNodes.UpdateRange(nodes);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.StructureNodes
            .AnyAsync(x => x.Id == id);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<List<StructureNode>> GetDescendantsAsync(string path)
    {
        return await _context.StructureNodes
            .Where(x =>
                x.Path.StartsWith(path) &&
                !x.IsDeleted)
            .ToListAsync();
    }

    public Task UpdateRangeAsync(List<StructureNode> nodes)
    {
        _context.StructureNodes.UpdateRange(nodes);

        return Task.CompletedTask;
    }

    public async Task<List<StructureNode>> GetChildrenOnlyAsync(Guid parentId)
    {
        return await _context.StructureNodes
            .Where(x =>
                x.ParentId == parentId &&
                !x.IsDeleted)
            .OrderBy(x => x.Order)
            .ToListAsync();
    }

    public async Task<List<StructureNode>> GetByIdsAsync(List<Guid> ids)
    {
        return await _context.StructureNodes
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();
    }

    public async Task<List<StructureNode>> GetDescendantsTreeAsync(string path)
    {
        return await _context.StructureNodes
            .Where(x =>
                x.Path.StartsWith(path) &&
                !x.IsDeleted)
            .OrderBy(x => x.Depth)
            .ThenBy(x => x.Order)
            .ToListAsync();
    }

    public async Task<List<StructureNode>> GetAncestorsAsync(List<Guid> ids)
    {
        return await _context.StructureNodes
            .Where(x =>
                ids.Contains(x.Id) &&
                !x.IsDeleted)
            .ToListAsync();
    }

    public async Task<List<StructureNode>> GetSiblingsAsync(Guid? parentId)
    {
        return await _context.StructureNodes
            .Where(x =>
                x.ParentId == parentId &&
                !x.IsDeleted)
            .OrderBy(x => x.Order)
            .ToListAsync();
    }
}
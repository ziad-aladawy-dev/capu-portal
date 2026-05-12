using CapitalUniversity.Core.Domain.Repositories;
using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using CapitalUniversity.Core.Application.UniversityStructure.UniversityStructureRules;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Application.UniversityStructure;

public class UniversityStructureService : IUniversityStructureService
{
    private readonly IStructureNodeRepository _repository;

    public UniversityStructureService(
        IStructureNodeRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<StructureNodeDto>> GetTreeAsync()
    {
        var allNodes = await _repository.GetAllAsync();

        var dtoMap = allNodes.ToDictionary(
            x => x.Id,
            x => new StructureNodeDto
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type,
                ParentId = x.ParentId,
                Order = x.Order,
                Path = x.Path,
                Depth = x.Depth,
                IsActive = x.IsActive,
                Children = new List<StructureNodeDto>()
            });

        foreach (var node in dtoMap.Values)
        {
            if (node.ParentId.HasValue &&
                dtoMap.ContainsKey(node.ParentId.Value))
            {
                dtoMap[node.ParentId.Value]
                    .Children
                    .Add(node);
            }
        }

        return dtoMap.Values
            .Where(x => x.ParentId == null)
            .OrderBy(x => x.Order)
            .ToList();
    }

    public async Task<StructureNodeDto?> GetByIdAsync(Guid id)
    {
        var allNodes = await _repository.GetAllAsync();

        var dtoMap = allNodes.ToDictionary(
            x => x.Id,
            x => new StructureNodeDto
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type,
                ParentId = x.ParentId,
                Order = x.Order,
                Path = x.Path,
                Depth = x.Depth,
                IsActive = x.IsActive,
                Children = new List<StructureNodeDto>()
            });

        foreach (var node in dtoMap.Values)
        {
            if (node.ParentId.HasValue &&
                dtoMap.ContainsKey(node.ParentId.Value))
            {
                dtoMap[node.ParentId.Value]
                    .Children
                    .Add(node);
            }
        }

        return dtoMap.ContainsKey(id)
            ? dtoMap[id]
            : null;
    }

    public async Task<Guid> CreateNodeAsync(CreateStructureNodeRequest request)
    {
        int depth = 0;

        string path;

        if (request.ParentId == null)
        {
            if (!StructureNodeValidator
                .CanBeRoot(request.Type))
            {
                throw new Exception(
                    "Only University can be root");
            }

            path = "";
        }
        else
        {
            var parent = await _repository.GetByIdAsync(
                request.ParentId.Value);

            if (parent == null)
                throw new Exception("Parent node not found");

            bool isValid = StructureNodeValidator
                .IsValidChild(
                    parent.Type,
                    request.Type);

            if (!isValid)
            {
                throw new Exception(
                    $"{request.Type} cannot be added under {parent.Type}");
            }

            depth = parent.Depth + 1;

            path = parent.Path;
        }

        var node = new StructureNode
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            ParentId = request.ParentId,
            Order = request.Order,
            Depth = depth,
            IsActive = true
        };

        node.Path = $"{path}/{node.Id}";

        await _repository.AddAsync(node);

        await _repository.SaveChangesAsync();

        return node.Id;
    }

    public async Task UpdateNodeAsync(
        Guid id,
        UpdateStructureNodeRequest request)
    {
        var node = await _repository.GetByIdAsync(id);

        if (node == null)
            throw new Exception("Node not found");

        node.Name = request.Name;
        node.Type = request.Type;
        node.Order = request.Order;
        node.IsActive = request.IsActive;

        await _repository.UpdateAsync(node);

        await _repository.SaveChangesAsync();
    }

    public async Task DeleteNodeAsync(Guid id)
    {
        var node = await _repository.GetByIdAsync(id);

        if (node == null)
            throw new Exception("Node not found");

        await _repository.RecursiveSoftDeleteAsync(node.Path);

        await _repository.SaveChangesAsync();
    }

    public async Task MoveNodeAsync(Guid nodeId, MoveStructureNodeRequest request)
    {
        var node = await _repository.GetByIdAsync(nodeId);

        if (node == null)
            throw new Exception("Node not found");

        if (request.NewParentId == node.Id)
            throw new Exception(
                "Node cannot be moved inside itself");

        StructureNode? newParent = null;

        if (request.NewParentId.HasValue)
        {
            newParent = await _repository.GetByIdAsync(
                request.NewParentId.Value);

            if (newParent == null)
                throw new Exception("New parent not found");

            bool isValid = StructureNodeValidator
                .IsValidChild(
                    newParent.Type,
                    node.Type);

            if (!isValid)
            {
                throw new Exception(
                    $"{node.Type} cannot be moved under {newParent.Type}");
            }

            if (newParent.Path.StartsWith(node.Path))
            {
                throw new Exception(
                    "Cannot move node into its descendants");
            }
        }

        var oldPath = node.Path;

        string newPath;

        int newDepth;

        if (newParent == null)
        {
            newDepth = 0;

            newPath = $"/{node.Id}";
        }
        else
        {
            newDepth = newParent.Depth + 1;

            newPath = $"{newParent.Path}/{node.Id}";
        }

        node.ParentId = request.NewParentId;
        node.Order = request.Order;
        node.Depth = newDepth;
        node.Path = newPath;
        node.UpdatedAt = DateTime.UtcNow;

        var descendants = await _repository
            .GetDescendantsAsync(oldPath);

        foreach (var descendant in descendants)
        {
            if (descendant.Id == node.Id)
                continue;

            descendant.Path = descendant.Path.Replace(
                oldPath,
                newPath);

            descendant.Depth =
                descendant.Path.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries)
                .Length - 1;

            descendant.UpdatedAt = DateTime.UtcNow;
        }

        descendants[0] = node;

        await _repository.UpdateRangeAsync(descendants);

        await _repository.SaveChangesAsync();
    }

    public async Task<List<StructureNodeDto>> GetRootsAsync()
    {
        var roots = await _repository.GetRootsAsync();

        return roots.Select(x => new StructureNodeDto
        {
            Id = x.Id,
            Name = x.Name,
            Type = x.Type,
            ParentId = x.ParentId,
            Order = x.Order,
            Path = x.Path,
            Depth = x.Depth,
            IsActive = x.IsActive
        }).ToList();
    }

    public async Task<List<StructureNodeDto>> GetChildrenAsync(Guid parentId)
    {
        var children = await _repository
            .GetChildrenOnlyAsync(parentId);

        return children.Select(x => new StructureNodeDto
        {
            Id = x.Id,
            Name = x.Name,
            Type = x.Type,
            ParentId = x.ParentId,
            Order = x.Order,
            Path = x.Path,
            Depth = x.Depth,
            IsActive = x.IsActive
        }).ToList();
    }

    public async Task<List<BreadcrumbItemDto>> GetBreadcrumbAsync(Guid nodeId)
    {
        var node = await _repository.GetByIdAsync(nodeId);

        if (node == null)
            throw new Exception("Node not found");

        var ids = node.Path
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries)
            .Select(Guid.Parse)
            .ToList();

        var nodes = await _repository.GetByIdsAsync(ids);

        var orderedNodes = ids
            .Select(id => nodes.First(x => x.Id == id))
            .ToList();

        return orderedNodes.Select(x =>
            new BreadcrumbItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type
            })
            .ToList();
    }

    public async Task<StructureNodeDto?> GetSubTreeAsync(Guid nodeId)
    {
        var node = await _repository.GetByIdAsync(nodeId);

        if (node == null)
            return null;

        var allNodes = await _repository
            .GetDescendantsTreeAsync(node.Path);

        var dtoMap = allNodes.ToDictionary(
            x => x.Id,
            x => new StructureNodeDto
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type,
                ParentId = x.ParentId,
                Order = x.Order,
                Path = x.Path,
                Depth = x.Depth,
                IsActive = x.IsActive,
                Children = new List<StructureNodeDto>()
            });

        foreach (var item in dtoMap.Values)
        {
            if (item.ParentId.HasValue &&
                dtoMap.ContainsKey(item.ParentId.Value))
            {
                dtoMap[item.ParentId.Value]
                    .Children
                    .Add(item);
            }
        }

        return dtoMap[nodeId];
    }

    public async Task<List<StructureNodeDto>> GetAncestorsChainAsync(Guid nodeId)
    {
        var node = await _repository.GetByIdAsync(nodeId);

        if (node == null)
            throw new Exception("Node not found");

        var ids = node.Path
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries)
            .Select(Guid.Parse)
            .ToList();

        ids.Remove(node.Id);

        var ancestors = await _repository
            .GetAncestorsAsync(ids);

        var ordered = ids
            .Select(id =>
                ancestors.First(x => x.Id == id))
            .ToList();

        return ordered.Select(x =>
        new StructureNodeDto
        {
            Id = x.Id,
            Name = x.Name,
            Type = x.Type,
            ParentId = x.ParentId,
            Order = x.Order,
            Path = x.Path,
            Depth = x.Depth,
            IsActive = x.IsActive,
            Children = new List<StructureNodeDto>()
        })
        .ToList();
    }

    public async Task ReorderNodeAsync(Guid nodeId, ReorderNodeRequest request)
    {
        var node = await _repository.GetByIdAsync(nodeId);

        if (node == null)
            throw new Exception("Node not found");

        var siblings = await _repository
            .GetSiblingsAsync(node.ParentId);

        siblings.RemoveAll(x => x.Id == node.Id);

        int targetOrder = request.NewOrder;

        if (targetOrder < 0)
            targetOrder = 0;

        if (targetOrder > siblings.Count)
            targetOrder = siblings.Count;

        siblings.Insert(targetOrder, node);

        for (int i = 0; i < siblings.Count; i++)
        {
            siblings[i].Order = i;
            siblings[i].UpdatedAt = DateTime.UtcNow;
        }

        await _repository.UpdateRangeAsync(siblings);

        await _repository.SaveChangesAsync();
    }
}
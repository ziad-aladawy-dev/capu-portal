using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using CapitalUniversity.Core.Application.DTOs.UniversityStructure;
using CapitalUniversity.Core.Application.UniversityStructure.UniversityStructureRules;
using CapitalUniversity.Core.Domain.UniversityStructure;
using System.Text.Json;

namespace CapitalUniversity.Core.Application.UniversityStructure;

public class UniversityStructureService : IUniversityStructureService
{
    private readonly IStructureNodeRepository _repository;
    private readonly ILocalizationService _localizationService;

    public UniversityStructureService(
        IStructureNodeRepository repository, ILocalizationService localizationService)
    {
        _repository = repository;
        _localizationService = localizationService;
    }

    public async Task<List<StructureNodeDto>> GetTreeAsync()
    {
        var allNodes = await _repository.GetAllAsync();

        var dtoMap = allNodes.ToDictionary(x => x.Id, MapToDto);

        foreach (var node in dtoMap.Values)
        {
            if (node.ParentId.HasValue && dtoMap.ContainsKey(node.ParentId.Value))
            {
                dtoMap[node.ParentId.Value].Children.Add(node);
            }
        }

        return dtoMap.Values
            .Where(x => x.ParentId == null)
            .OrderBy(x => x.Order)
            .ToList();
    }

    public async Task<StructureNodeDto?> GetByIdAsync(Guid id)
    {
        var node = await _repository.GetByIdAsync(id);
        return node == null ? null : MapToDto(node);
    }

    //public async Task<StructureNodeDto?> GetByIdAsync(Guid id)
    //{
    //    var allNodes = await _repository.GetAllAsync();

    //    var dtoMap = allNodes.ToDictionary(
    //        x => x.Id,
    //        x => new StructureNodeDto
    //        {
    //            Id = x.Id,
    //            Name = x.Name,
    //            Type = x.Type,
    //            ParentId = x.ParentId,
    //            Order = x.Order,
    //            Path = x.Path,
    //            Depth = x.Depth,
    //            IsActive = x.IsActive,
    //            Children = new List<StructureNodeDto>()
    //        });

    //    foreach (var node in dtoMap.Values)
    //    {
    //        if (node.ParentId.HasValue &&
    //            dtoMap.ContainsKey(node.ParentId.Value))
    //        {
    //            dtoMap[node.ParentId.Value]
    //                .Children
    //                .Add(node);
    //        }
    //    }

    //    return dtoMap.ContainsKey(id)
    //        ? dtoMap[id]
    //        : null;
    //}

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

        var nameJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "ar", request.Name },
            { "en", request.NameEn }
        });

        var node = new StructureNode
        {
            Id = Guid.NewGuid(),
            Name = nameJson,
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

    public async Task UpdateNodeAsync(Guid id, UpdateStructureNodeRequest request)
    {
        var node = await _repository.GetByIdAsync(id);

        if (node == null)
            throw new Exception("Node not found");

        if (!string.IsNullOrEmpty(request.Name))
        {
            if (!string.IsNullOrEmpty(request.NameEn))
            {
                var dict = new Dictionary<string, string>();
                try { dict = JsonSerializer.Deserialize<Dictionary<string, string>>(node.Name) ?? new(); }
                catch { dict = new(); }
                dict["ar"] = request.Name;
                dict["en"] = request.NameEn;
                node.Name = JsonSerializer.Serialize(dict);
            }
            else
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(node.Name) ?? new();
                dict["ar"] = request.Name;
                node.Name = JsonSerializer.Serialize(dict);
            }
        }
        else if (!string.IsNullOrEmpty(request.NameEn))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(node.Name) ?? new();
            dict["en"] = request.NameEn;
            node.Name = JsonSerializer.Serialize(dict);
        }

        node.Type = request.Type;
        node.Order = request.Order;
        node.IsActive = request.IsActive;
        node.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(node);
        await _repository.SaveChangesAsync();
    }
    //    node.Name = request.Name;
    //    node.Type = request.Type;
    //    node.Order = request.Order;
    //    node.IsActive = request.IsActive;

    //    await _repository.UpdateAsync(node);

    //    await _repository.SaveChangesAsync();
    //}

    public async Task DeleteNodeAsync(Guid id)
    {
        var node = await _repository.GetByIdAsync(id);

        if (node == null)
            throw new Exception("Node not found");

        await _repository.RecursiveSoftDeleteAsync(node.Path);

        await _repository.SaveChangesAsync();
    }

    public async Task MoveNodeAsync(
        Guid nodeId,
        MoveStructureNodeRequest request)
    {
        var node =
            await _repository.GetByIdAsync(nodeId);

        if (node == null || node.IsDeleted)
        {
            throw new Exception("Node not found");
        }

        if (request.NewParentId == node.Id)
        {
            throw new Exception(
                "Node cannot be moved inside itself");
        }

        StructureNode? newParent = null;

        if (request.NewParentId.HasValue)
        {
            newParent =
                await _repository.GetByIdAsync(
                    request.NewParentId.Value);

            if (newParent == null ||
                newParent.IsDeleted)
            {
                throw new Exception(
                    "New parent not found");
            }

            if (!newParent.IsActive)
            {
                throw new Exception(
                    "Cannot move under inactive node");
            }

            bool isValid =
                StructureNodeValidator
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
                    "Cannot move node inside descendants");
            }
        }

        string oldPath = node.Path;

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

            newPath =
                $"{newParent.Path}/{node.Id}";
        }

        node.ParentId =
            request.NewParentId;

        node.Order =
            request.Order;

        node.Path =
            newPath;

        node.Depth =
            newDepth;

        node.UpdatedAt =
            DateTime.UtcNow;

        var descendants =
            await _repository
                .GetDescendantsAsync(oldPath);

        foreach (var item in descendants)
        {
            if (item.Id == node.Id)
                continue;

            item.Path =
                item.Path.Replace(
                    oldPath,
                    newPath);

            item.Depth =
                item.Path.Split(
                    '/',
                    StringSplitOptions
                        .RemoveEmptyEntries)
                .Length - 1;

            item.UpdatedAt =
                DateTime.UtcNow;
        }

        descendants[0] = node;

        await _repository
            .UpdateRangeAsync(descendants);

        await _repository
            .SaveChangesAsync();
    }

    public async Task<List<StructureNodeDto>> GetRootsAsync()
    {
        var roots = await _repository.GetRootsAsync();

        return roots.Select(MapToDto).ToList();
    }

    public async Task<List<StructureNodeDto>> GetChildrenAsync(Guid parentId)
    {
        var children = await _repository.GetChildrenOnlyAsync(parentId);

        return children.Select(MapToDto).ToList();
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
                Name = _localizationService.GetLocalizedString(x.Name),
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

        var dtoMap = allNodes.ToDictionary(x => x.Id, MapToDto);

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

        return ordered.Select(MapToDto).ToList();
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

    private StructureNodeDto MapToDto(StructureNode node)
    {
        return new StructureNodeDto
        {
            Id = node.Id,
            Name = node.Name,
            LocalizedName = _localizationService.GetLocalizedString(node.Name),
            Type = node.Type,
            ParentId = node.ParentId,
            Order = node.Order,
            Path = node.Path,
            Depth = node.Depth,
            IsActive = node.IsActive,
            TypeNameLocalized = _localizationService.Get(node.Type),
            Children = new List<StructureNodeDto>()
        };
    }
}
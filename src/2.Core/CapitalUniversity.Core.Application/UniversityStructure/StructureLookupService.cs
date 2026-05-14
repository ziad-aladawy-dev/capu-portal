using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.Application.UniversityStructure;

public class StructureLookupService : IStructureLookupService
{
    private readonly IStructureNodeRepository _repository;

    public StructureLookupService(
        IStructureNodeRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<StructureNodeLookupDto>>
        GetByTypeAsync(
            StructureNodeType type)
    {
        var nodes = await _repository.GetAllAsync();

        return nodes
            .Where(x => x.Type == type)
            .OrderBy(x => x.Order)
            .Select(Map)
            .ToList();
    }

    public async Task<List<StructureNodeLookupDto>>
        GetChildrenAsync(Guid parentId)
    {
        var nodes = await _repository.GetAllAsync();

        return nodes
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Order)
            .Select(Map)
            .ToList();
    }

    public async Task<List<StructureNodeLookupDto>>
        GetChildrenByTypeAsync(
            Guid parentId,
            StructureNodeType type)
    {
        var nodes = await _repository.GetAllAsync();

        return nodes
            .Where(x =>
                x.ParentId == parentId &&
                x.Type == type)
            .OrderBy(x => x.Order)
            .Select(Map)
            .ToList();
    }

    private static StructureNodeLookupDto Map(
        Domain.UniversityStructure.StructureNode node)
    {
        return new StructureNodeLookupDto
        {
            Id = node.Id,

            Name = node.Name,

            Type = (int)node.Type,

            ParentId = node.ParentId
        };
    }
}
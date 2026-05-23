using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.Application.UniversityStructure;

public class StructureLookupService : IStructureLookupService
{
    private readonly IStructureNodeRepository _repository;
    private readonly ILocalizationService _localization;

    public StructureLookupService(
        IStructureNodeRepository repository,
        ILocalizationService localization)
    {
        _repository = repository;
        _localization = localization;
    }

    public async Task<List<StructureNodeLookupDto>> GetByTypeAsync(StructureNodeType type)
    {
        var nodes = await _repository.GetAllAsync();

        return nodes.Where(x => x.Type == type).OrderBy(x => x.Order).Select(Map).ToList();
    }

    public async Task<List<StructureNodeLookupDto>> GetChildrenAsync(Guid parentId)
    {
        var nodes = await _repository.GetAllAsync();

        return nodes.Where(x => x.ParentId == parentId).OrderBy(x => x.Order).Select(Map).ToList();
    }

    public async Task<List<StructureNodeLookupDto>> GetChildrenByTypeAsync(Guid parentId, StructureNodeType type)
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

    public async Task<List<StructureNodeLookupDto>> GetProgramsByFacultyAsync(Guid facultyId)
    {
        var nodes = await _repository.GetAllAsync();
        var nodeMap = nodes.ToLookup(x => x.ParentId);
        var programs = new List<Domain.UniversityStructure.StructureNode>();

        void CollectPrograms(Guid parentId)
        {
            foreach (var child in nodeMap[parentId])
            {
                if (child.Type == StructureNodeType.Program)
                    programs.Add(child);
                CollectPrograms(child.Id);
            }
        }

        CollectPrograms(facultyId);

        return programs.OrderBy(x => x.Order).Select(Map).ToList();
    }

    private StructureNodeLookupDto Map(
        Domain.UniversityStructure.StructureNode node)
    {
        return new StructureNodeLookupDto
        {
            Id = node.Id,
            Name = _localization.Get<string>(node.Name),
            LocalizedName = _localization.Get<string>(node.Name),
            Type = (int)node.Type,
            ParentId = node.ParentId
        };
    }
}
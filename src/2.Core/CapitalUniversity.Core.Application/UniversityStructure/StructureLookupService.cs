using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Application.UniversityStructure;

public class StructureLookupService : IStructureLookupService
{
    private readonly IStructureNodeRepository _repository;
    private readonly ILocalizationService _localizationService;

    public StructureLookupService(
        IStructureNodeRepository repository, ILocalizationService localizationService)
    {
        _repository = repository;
        _localizationService = localizationService;
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

        return nodes.Where(x => x.ParentId == parentId && x.Type == type).OrderBy(x => x.Order).Select(Map).ToList();
    }

    public async Task<List<StructureNodeLookupDto>> GetProgramsByFacultyAsync(Guid facultyId)
    {
        var facultyNode = await _repository.GetByIdAsync(facultyId);
        if (facultyNode == null)
            return new List<StructureNodeLookupDto>();

        var descendants = await _repository.GetDescendantsAsync(facultyNode.Path);

        var programs = descendants
            .Where(x => x.Type == StructureNodeType.Program)
            .OrderBy(x => x.Order)
            .ToList();

        return programs.Select(Map).ToList();
    }

    private StructureNodeLookupDto Map(StructureNode node)
    {
        return new StructureNodeLookupDto
        {
            Id = node.Id,

            Name = node.Name,

            LocalizedName = _localizationService.GetLocalizedString(node.Name),

            Type = (int)node.Type,

            ParentId = node.ParentId
        };
    }
}
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.Abstractions.UniversityStructure;

public interface IStructureLookupService
{
    Task<List<StructureNodeLookupDto>> GetByTypeAsync(StructureNodeType type);

    Task<List<StructureNodeLookupDto>> GetChildrenAsync(Guid parentId);

    Task<List<StructureNodeLookupDto>> GetChildrenByTypeAsync(Guid parentId, StructureNodeType type);
}
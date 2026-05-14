using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;

public class UpdateStructureNodeRequest
{
    public string Name { get; set; } = string.Empty;

    public StructureNodeType Type { get; set; }

    public int Order { get; set; }

    public bool IsActive { get; set; }
}

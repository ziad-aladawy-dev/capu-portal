using CapitalUniversity.Core.Domain.Common;
<<<<<<< Updated upstream
=======
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
>>>>>>> Stashed changes

namespace CapitalUniversity.Core.Domain.UniversityStructure;

public class StructureNode : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public StructureNodeType Type { get; set; }

    public Guid? ParentId { get; set; }

    public int Order { get; set; }

    public string Path { get; set; } = string.Empty;

    public int Depth { get; set; }

    public bool IsActive { get; set; } = true;

    public StructureNode? Parent { get; set; }

    public ICollection<StructureNode> Children { get; set; } = new List<StructureNode>();
}
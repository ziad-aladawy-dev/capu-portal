using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class ServiceStructureNode : BaseEntity
{
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public Guid StructureNodeId { get; set; }
    public StructureNode StructureNode { get; set; } = null!;
}
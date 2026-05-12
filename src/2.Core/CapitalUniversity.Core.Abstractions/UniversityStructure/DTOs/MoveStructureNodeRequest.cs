namespace CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;

public class MoveStructureNodeRequest
{
    public Guid? NewParentId { get; set; }

    public int Order { get; set; }
}
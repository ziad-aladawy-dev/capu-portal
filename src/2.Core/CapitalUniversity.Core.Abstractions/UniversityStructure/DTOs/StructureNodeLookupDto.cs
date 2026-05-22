namespace CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;

public class StructureNodeLookupDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string LocalizedName { get; set; } = string.Empty;

    public int Type { get; set; }

    public Guid? ParentId { get; set; }
}
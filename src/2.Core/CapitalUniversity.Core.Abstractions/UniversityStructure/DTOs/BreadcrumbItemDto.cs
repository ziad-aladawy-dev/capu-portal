using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.Application.DTOs.UniversityStructure;

public class BreadcrumbItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public StructureNodeType Type { get; set; }
}
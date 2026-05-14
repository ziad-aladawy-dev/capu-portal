<<<<<<< Updated upstream
using CapitalUniversity.Core.Domain.UniversityStructure;
=======
﻿using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
>>>>>>> Stashed changes

namespace CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;

public class StructureNodeDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public StructureNodeType Type { get; set; }

    public Guid? ParentId { get; set; }

    public int Order { get; set; }

    public string Path { get; set; } = string.Empty;

    public int Depth { get; set; }

    public bool IsActive { get; set; }

    public List<StructureNodeDto> Children { get; set; } = new();
}

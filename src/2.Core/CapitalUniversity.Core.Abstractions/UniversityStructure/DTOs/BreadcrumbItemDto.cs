<<<<<<< Updated upstream
using CapitalUniversity.Core.Domain.UniversityStructure;
=======
﻿using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
>>>>>>> Stashed changes

namespace CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;

public class BreadcrumbItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public StructureNodeType Type { get; set; }
}

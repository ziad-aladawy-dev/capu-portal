<<<<<<< Updated upstream
using CapitalUniversity.Core.Domain.UniversityStructure;
=======
﻿using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
>>>>>>> Stashed changes

namespace CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;

public class CreateStructureNodeRequest
{
    public string Name { get; set; } = string.Empty;

    public StructureNodeType Type { get; set; }

    public Guid? ParentId { get; set; }

    public int Order { get; set; }
}

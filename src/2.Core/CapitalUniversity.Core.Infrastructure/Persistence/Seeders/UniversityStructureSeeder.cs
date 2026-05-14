<<<<<<< Updated upstream
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure;
=======
﻿using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
>>>>>>> Stashed changes


namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class UniversityStructureSeeder
{
    public static async Task SeedAsync(
        CoreDbContext context)
    {
        if (context.StructureNodes.Any())
            return;

        var nodes = new List<StructureNode>();

        var university = CreateNode(
            "Capital University",
            StructureNodeType.University,
            null,
            0,
            nodes);

        var commerceFaculty = CreateNode(
            "Faculty of Commerce",
            StructureNodeType.Faculty,
            university,
            0,
            nodes);

        var csFaculty = CreateNode(
            "Faculty of Computers and AI",
            StructureNodeType.Faculty,
            university,
            1,
            nodes);

        var mediaFaculty = CreateNode(
            "Faculty of Media",
            StructureNodeType.Faculty,
            university,
            2,
            nodes);

        var commerceCreditHours = CreateNode(
            "Credit Hours System",
            StructureNodeType.System,
            commerceFaculty,
            0,
            nodes);

        var commerceRegular = CreateNode(
            "Regular System",
            StructureNodeType.System,
            commerceFaculty,
            1,
            nodes);

        var accountingProgram = CreateNode(
            "Accounting Program",
            StructureNodeType.Program,
            commerceCreditHours,
            0,
            nodes);

        var businessProgram = CreateNode(
            "Business Administration Program",
            StructureNodeType.Program,
            commerceCreditHours,
            1,
            nodes);

        var level1 = CreateNode(
            "First Level",
            StructureNodeType.Level,
            accountingProgram,
            0,
            nodes);

        var level2 = CreateNode(
            "Second Level",
            StructureNodeType.Level,
            accountingProgram,
            1,
            nodes);

        var accountingSpec = CreateNode(
            "Accounting Specialization",
            StructureNodeType.Specialization,
            level2,
            0,
            nodes);

        var financeSpec = CreateNode(
            "Finance Specialization",
            StructureNodeType.Specialization,
            level2,
            1,
            nodes);

        CreateNode(
            "Third Level",
            StructureNodeType.Level,
            accountingSpec,
            0,
            nodes);

        CreateNode(
            "Fourth Level",
            StructureNodeType.Level,
            accountingSpec,
            1,
            nodes);

        var csCreditHours = CreateNode(
            "Credit Hours System",
            StructureNodeType.System,
            csFaculty,
            0,
            nodes);

        var aiProgram = CreateNode(
            "Artificial Intelligence Program",
            StructureNodeType.Program,
            csCreditHours,
            0,
            nodes);

        var cyberProgram = CreateNode(
            "Cyber Security Program",
            StructureNodeType.Program,
            csCreditHours,
            1,
            nodes);

        CreateNode(
            "First Level",
            StructureNodeType.Level,
            aiProgram,
            0,
            nodes);

        CreateNode(
            "Second Level",
            StructureNodeType.Level,
            aiProgram,
            1,
            nodes);

        CreateNode(
            "First Level",
            StructureNodeType.Level,
            cyberProgram,
            0,
            nodes);

        await context.StructureNodes.AddRangeAsync(nodes);

        await context.SaveChangesAsync();
    }

    private static StructureNode CreateNode(
        string name,
        StructureNodeType type,
        StructureNode? parent,
        int order,
        List<StructureNode> nodes)
    {
        var node = new StructureNode
        {
            Id = Guid.NewGuid(),

            Name = name,

            Type = type,

            ParentId = parent?.Id,

            Parent = parent,

            Order = order,

            Depth = parent == null
                ? 0
                : parent.Depth + 1,

            IsActive = true
        };

        node.Path = parent == null
            ? $"/{node.Id}"
            : $"{parent.Path}/{node.Id}";

        nodes.Add(node);

        return node;
    }
}

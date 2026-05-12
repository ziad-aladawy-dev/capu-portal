using CapitalUniversity.Core.Abstractions.UniversityStructure.Enums;
using CapitalUniversity.Core.Domain.UniversityStructure;


namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class UniversityStructureSeeder
{
    public static async Task SeedAsync(
        CoreDbContext context)
    {
        if (context.StructureNodes.Any())
            return;

        var university = CreateNode(
            "Capital University",
            StructureNodeType.University,
            null,
            0);

        var commerceFaculty = CreateNode(
            "Faculty of Commerce",
            StructureNodeType.Faculty,
            university,
            0);

        var csFaculty = CreateNode(
            "Faculty of Computers and AI",
            StructureNodeType.Faculty,
            university,
            1);

        var mediaFaculty = CreateNode(
            "Faculty of Media",
            StructureNodeType.Faculty,
            university,
            2);

        var commerceCreditHours = CreateNode(
            "Credit Hours System",
            StructureNodeType.System,
            commerceFaculty,
            0);

        var commerceRegular = CreateNode(
            "Regular System",
            StructureNodeType.System,
            commerceFaculty,
            1);

        var accountingProgram = CreateNode(
            "Accounting Program",
            StructureNodeType.Program,
            commerceCreditHours,
            0);

        var businessProgram = CreateNode(
            "Business Administration Program",
            StructureNodeType.Program,
            commerceCreditHours,
            1);

        var level1 = CreateNode(
            "First Level",
            StructureNodeType.Level,
            accountingProgram,
            0);

        var level2 = CreateNode(
            "Second Level",
            StructureNodeType.Level,
            accountingProgram,
            1);

        var accountingSpec = CreateNode(
            "Accounting Specialization",
            StructureNodeType.Specialization,
            level2,
            0);

        var financeSpec = CreateNode(
            "Finance Specialization",
            StructureNodeType.Specialization,
            level2,
            1);

        CreateNode(
            "Third Level",
            StructureNodeType.Level,
            accountingSpec,
            0);

        CreateNode(
            "Fourth Level",
            StructureNodeType.Level,
            accountingSpec,
            1);

        var csCreditHours = CreateNode(
            "Credit Hours System",
            StructureNodeType.System,
            csFaculty,
            0);

        var aiProgram = CreateNode(
            "Artificial Intelligence Program",
            StructureNodeType.Program,
            csCreditHours,
            0);

        var cyberProgram = CreateNode(
            "Cyber Security Program",
            StructureNodeType.Program,
            csCreditHours,
            1);

        CreateNode(
            "First Level",
            StructureNodeType.Level,
            aiProgram,
            0);

        CreateNode(
            "Second Level",
            StructureNodeType.Level,
            aiProgram,
            1);

        CreateNode(
            "First Level",
            StructureNodeType.Level,
            cyberProgram,
            0);

        await context.StructureNodes.AddRangeAsync(_nodes);

        await context.SaveChangesAsync();
    }

    private static readonly List<StructureNode> _nodes = new();

    private static StructureNode CreateNode(
        string name,
        StructureNodeType type,
        StructureNode? parent,
        int order)
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

        _nodes.Add(node);

        return node;
    }
}
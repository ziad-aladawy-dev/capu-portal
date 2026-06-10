using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Application.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.UniversityStructure;

/// <summary>
/// StructureLookupService projects structure nodes onto lookup DTOs, filtering
/// by type / parent and ordering by <c>Order</c>. These tests pin the filter
/// predicates, the ascending order, the int cast on Type, and the localized
/// Name decode so conditional / ordering / projection mutations are killed.
/// </summary>
public class StructureLookupServiceTests
{
    private static (StructureLookupService Sut, Mock<IStructureNodeRepository> Repo) Build()
    {
        var repo = new Mock<IStructureNodeRepository>();
        var sut = new StructureLookupService(repo.Object, new TestLocalizationService());
        return (sut, repo);
    }

    private static StructureNode Node(StructureNodeType type, int order, Guid? parentId = null, string en = "Node") => new()
    {
        Type = type,
        Order = order,
        ParentId = parentId,
        Name = $"{{\"ar\":\"اسم\",\"en\":\"{en}\"}}",
    };

    // ---------------- GetByTypeAsync ----------------

    [Fact]
    public async Task GetByType_FiltersByTypeAndOrdersAscending()
    {
        var (sut, repo) = Build();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<StructureNode>
        {
            Node(StructureNodeType.Faculty, 2, en: "B"),
            Node(StructureNodeType.Program, 0, en: "Prog"),
            Node(StructureNodeType.Faculty, 1, en: "A"),
        });

        var result = await sut.GetByTypeAsync(StructureNodeType.Faculty);

        result.Should().HaveCount(2);
        result.Select(x => x.LocalizedName).Should().ContainInOrder("A", "B");
        result.All(x => x.Type == (int)StructureNodeType.Faculty).Should().BeTrue();
    }

    [Fact]
    public async Task GetByType_DecodesLocalizedNameAndCastsType()
    {
        var (sut, repo) = Build();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<StructureNode>
        {
            Node(StructureNodeType.Level, 0, en: "First Level"),
        });

        var dto = (await sut.GetByTypeAsync(StructureNodeType.Level)).Single();

        dto.LocalizedName.Should().Be("First Level");
        dto.Type.Should().Be((int)StructureNodeType.Level);
    }

    [Fact]
    public async Task GetByType_NoMatch_ReturnsEmpty()
    {
        var (sut, repo) = Build();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<StructureNode>
        {
            Node(StructureNodeType.Faculty, 0),
        });

        (await sut.GetByTypeAsync(StructureNodeType.Specialization)).Should().BeEmpty();
    }

    // ---------------- GetChildrenAsync ----------------

    [Fact]
    public async Task GetChildren_FiltersByParentAndOrders()
    {
        var (sut, repo) = Build();
        var parent = Guid.NewGuid();
        var other = Guid.NewGuid();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<StructureNode>
        {
            Node(StructureNodeType.Program, 1, parent, "Second"),
            Node(StructureNodeType.Program, 0, parent, "First"),
            Node(StructureNodeType.Program, 0, other, "Other"),
        });

        var result = await sut.GetChildrenAsync(parent);

        result.Should().HaveCount(2);
        result.Select(x => x.LocalizedName).Should().ContainInOrder("First", "Second");
        result.All(x => x.ParentId == parent).Should().BeTrue();
    }

    // ---------------- GetChildrenByTypeAsync ----------------

    [Fact]
    public async Task GetChildrenByType_FiltersByParentAndType()
    {
        var (sut, repo) = Build();
        var parent = Guid.NewGuid();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<StructureNode>
        {
            Node(StructureNodeType.Level, 0, parent, "Level"),
            Node(StructureNodeType.Program, 1, parent, "Program"),
            Node(StructureNodeType.Level, 2, Guid.NewGuid(), "OtherParentLevel"),
        });

        var result = await sut.GetChildrenByTypeAsync(parent, StructureNodeType.Level);

        result.Should().ContainSingle().Which.LocalizedName.Should().Be("Level");
    }

    [Fact]
    public async Task GetChildrenByType_WrongParentSameType_Excluded()
    {
        var (sut, repo) = Build();
        var parent = Guid.NewGuid();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<StructureNode>
        {
            Node(StructureNodeType.Level, 0, Guid.NewGuid(), "WrongParent"),
        });

        (await sut.GetChildrenByTypeAsync(parent, StructureNodeType.Level)).Should().BeEmpty();
    }
}

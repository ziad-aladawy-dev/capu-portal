using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using CapitalUniversity.Core.Application.DTOs.UniversityStructure;
using CapitalUniversity.Core.Application.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.UniversityStructure;

/// <summary>
/// Mutation-focused tests for <see cref="UniversityStructureService"/>: tree
/// assembly, hierarchy validation, path/depth math on create &amp; move,
/// descendant repath, breadcrumb/ancestor ordering, and sibling reordering.
/// </summary>
public class UniversityStructureServiceTests
{
    private static (UniversityStructureService sut,
                    Mock<IStructureNodeRepository> repo,
                    Mock<IUnitOfWork> uow,
                    Mock<IPermissionCacheInvalidator> cache) Build()
    {
        var repo = new Mock<IStructureNodeRepository>();
        var uow = new Mock<IUnitOfWork>();
        var loc = new Mock<ILocalizationService>();
        var cache = new Mock<IPermissionCacheInvalidator>();
        loc.Setup(l => l.Get<string>(It.IsAny<string>())).Returns<string>(s => s ?? string.Empty);
        var sut = new UniversityStructureService(repo.Object, uow.Object, loc.Object, cache.Object);
        return (sut, repo, uow, cache);
    }

    private static StructureNode Node(StructureNodeType type, string path, int depth = 0, Guid? parentId = null, int order = 0, bool active = true)
        => new() { Id = Guid.NewGuid(), Name = type.ToString(), Type = type, Path = path, Depth = depth, ParentId = parentId, Order = order, IsActive = active };

    // ── CreateNodeAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateNode_NonUniversityRoot_Throws()
    {
        var (sut, _, _, _) = Build();
        var act = () => sut.CreateNodeAsync(new CreateStructureNodeRequest { Type = StructureNodeType.Faculty, ParentId = null });
        await act.Should().ThrowAsync<Exception>().WithMessage("Only University can be root");
    }

    [Fact]
    public async Task CreateNode_UniversityRoot_PersistsDepthZeroAndRootPath()
    {
        var (sut, repo, uow, _) = Build();
        StructureNode? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<StructureNode>())).Callback<StructureNode>(n => saved = n);

        var id = await sut.CreateNodeAsync(new CreateStructureNodeRequest { Type = StructureNodeType.University, ParentId = null, Order = 2 });

        saved.Should().NotBeNull();
        saved!.Depth.Should().Be(0);
        saved.Path.Should().Be($"/{saved.Id}");
        saved.Order.Should().Be(2);
        saved.IsActive.Should().BeTrue();
        id.Should().Be(saved.Id);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateNode_ParentMissing_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);
        var act = () => sut.CreateNodeAsync(new CreateStructureNodeRequest { Type = StructureNodeType.Faculty, ParentId = Guid.NewGuid() });
        await act.Should().ThrowAsync<Exception>().WithMessage("Parent node not found");
    }

    [Fact]
    public async Task CreateNode_InvalidChildType_Throws()
    {
        var (sut, repo, _, _) = Build();
        var parent = Node(StructureNodeType.University, "/u", 0);
        repo.Setup(r => r.GetByIdAsync(parent.Id)).ReturnsAsync(parent);
        // University only allows Faculty; Level is invalid here.
        var act = () => sut.CreateNodeAsync(new CreateStructureNodeRequest { Type = StructureNodeType.Level, ParentId = parent.Id });
        await act.Should().ThrowAsync<Exception>().WithMessage("Level cannot be added under University");
    }

    [Fact]
    public async Task CreateNode_ValidChild_ComputesDepthAndPathFromParent()
    {
        var (sut, repo, _, _) = Build();
        var parent = Node(StructureNodeType.University, "/uni", depth: 0);
        repo.Setup(r => r.GetByIdAsync(parent.Id)).ReturnsAsync(parent);
        StructureNode? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<StructureNode>())).Callback<StructureNode>(n => saved = n);

        await sut.CreateNodeAsync(new CreateStructureNodeRequest { Type = StructureNodeType.Faculty, ParentId = parent.Id });

        saved!.Depth.Should().Be(1, "child depth is parent.Depth + 1");
        saved.Path.Should().Be($"/uni/{saved.Id}");
        saved.ParentId.Should().Be(parent.Id);
    }

    // ── UpdateNodeAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateNode_Missing_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);
        var act = () => sut.UpdateNodeAsync(Guid.NewGuid(), new UpdateStructureNodeRequest());
        await act.Should().ThrowAsync<Exception>().WithMessage("Node not found");
    }

    [Fact]
    public async Task UpdateNode_AppliesFieldsAndSaves()
    {
        var (sut, repo, uow, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/a/b", 1);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);

        await sut.UpdateNodeAsync(node.Id, new UpdateStructureNodeRequest { Type = StructureNodeType.System, Order = 9, IsActive = false, Name = "N" });

        node.Type.Should().Be(StructureNodeType.System);
        node.Order.Should().Be(9);
        node.IsActive.Should().BeFalse();
        repo.Verify(r => r.UpdateAsync(node), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ── DeleteNodeAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteNode_Missing_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);
        var act = () => sut.DeleteNodeAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<Exception>().WithMessage("Node not found");
    }

    [Fact]
    public async Task DeleteNode_RecursivelySoftDeletesByPath()
    {
        var (sut, repo, uow, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/uni/fac", 1);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);

        await sut.DeleteNodeAsync(node.Id);

        repo.Verify(r => r.RecursiveSoftDeleteAsync("/uni/fac"), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ── MoveNodeAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task MoveNode_Missing_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);
        var act = () => sut.MoveNodeAsync(Guid.NewGuid(), new MoveStructureNodeRequest());
        await act.Should().ThrowAsync<Exception>().WithMessage("Node not found");
    }

    [Fact]
    public async Task MoveNode_IntoItself_Throws()
    {
        var (sut, repo, _, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/u/f", 1);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        var act = () => sut.MoveNodeAsync(node.Id, new MoveStructureNodeRequest { NewParentId = node.Id });
        await act.Should().ThrowAsync<Exception>().WithMessage("Node cannot be moved inside itself");
    }

    [Fact]
    public async Task MoveNode_NewParentMissing_Throws()
    {
        var (sut, repo, _, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/u/f", 1);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        var newParentId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(newParentId)).ReturnsAsync((StructureNode?)null);
        var act = () => sut.MoveNodeAsync(node.Id, new MoveStructureNodeRequest { NewParentId = newParentId });
        await act.Should().ThrowAsync<Exception>().WithMessage("New parent not found");
    }

    [Fact]
    public async Task MoveNode_NewParentInactive_Throws()
    {
        var (sut, repo, _, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/u/f", 1);
        var newParent = Node(StructureNodeType.University, "/u2", 0, active: false);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetByIdAsync(newParent.Id)).ReturnsAsync(newParent);
        var act = () => sut.MoveNodeAsync(node.Id, new MoveStructureNodeRequest { NewParentId = newParent.Id });
        await act.Should().ThrowAsync<Exception>().WithMessage("Cannot move under inactive node");
    }

    [Fact]
    public async Task MoveNode_InvalidChildUnderNewParent_Throws()
    {
        var (sut, repo, _, _) = Build();
        var node = Node(StructureNodeType.Level, "/u/f/p/l", 3);
        var newParent = Node(StructureNodeType.University, "/u", 0); // University does not allow Level
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetByIdAsync(newParent.Id)).ReturnsAsync(newParent);
        var act = () => sut.MoveNodeAsync(node.Id, new MoveStructureNodeRequest { NewParentId = newParent.Id });
        await act.Should().ThrowAsync<Exception>().WithMessage("Level cannot be moved under University");
    }

    [Fact]
    public async Task MoveNode_UnderOwnDescendant_Throws()
    {
        var (sut, repo, _, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/u/f", 1);
        // newParent is a descendant: its path starts with the node's path, and
        // its type validly accepts the node's type (University accepts Faculty),
        // so the child-type guard passes and we reach the descendant guard.
        var newParent = Node(StructureNodeType.University, "/u/f/sub", 3);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetByIdAsync(newParent.Id)).ReturnsAsync(newParent);
        var act = () => sut.MoveNodeAsync(node.Id, new MoveStructureNodeRequest { NewParentId = newParent.Id });
        await act.Should().ThrowAsync<Exception>().WithMessage("Cannot move node inside descendants");
    }

    [Fact]
    public async Task MoveNode_ToRoot_SetsDepthZeroAndRepathsDescendants()
    {
        var (sut, repo, _, cache) = Build();
        var node = Node(StructureNodeType.Faculty, "/u/f", 1);
        var child = Node(StructureNodeType.Program, "/u/f/p", 2);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetDescendantsAsync("/u/f")).ReturnsAsync(new List<StructureNode> { node, child });

        await sut.MoveNodeAsync(node.Id, new MoveStructureNodeRequest { NewParentId = null, Order = 0 });

        node.Depth.Should().Be(0);
        node.Path.Should().Be($"/{node.Id}");
        child.Path.Should().Be($"/{node.Id}/p", "descendant paths get the old prefix rewritten");
        repo.Verify(r => r.RepairPermissionPathPrefixAsync("/u/f", $"/{node.Id}", It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.InvalidateAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MoveNode_UnderValidParent_ComputesDepthFromParent()
    {
        var (sut, repo, _, _) = Build();
        var node = Node(StructureNodeType.Program, "/u/f1/p", 2);
        var newParent = Node(StructureNodeType.Faculty, "/u/f2", 1);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetByIdAsync(newParent.Id)).ReturnsAsync(newParent);
        repo.Setup(r => r.GetDescendantsAsync("/u/f1/p")).ReturnsAsync(new List<StructureNode> { node });

        await sut.MoveNodeAsync(node.Id, new MoveStructureNodeRequest { NewParentId = newParent.Id });

        node.Depth.Should().Be(2, "newParent.Depth(1) + 1");
        node.Path.Should().Be($"/u/f2/{node.Id}");
    }

    // ── Read projections ────────────────────────────────────────────────

    [Fact]
    public async Task GetTreeAsync_NestsChildrenAndReturnsRootsOrdered()
    {
        var (sut, repo, _, _) = Build();
        var root1 = Node(StructureNodeType.University, "/r1", 0, order: 5);
        var root2 = Node(StructureNodeType.University, "/r2", 0, order: 1);
        var child = Node(StructureNodeType.Faculty, "/r1/c", 1, parentId: root1.Id);
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<StructureNode> { root1, child, root2 });

        var tree = await sut.GetTreeAsync();

        tree.Should().HaveCount(2);
        tree[0].Id.Should().Be(root2.Id, "roots ordered by Order ascending (1 before 5)");
        tree.Single(x => x.Id == root1.Id).Children.Should().ContainSingle(c => c.Id == child.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNode_OrNullWhenAbsent()
    {
        var (sut, repo, _, _) = Build();
        var n = Node(StructureNodeType.University, "/r", 0);
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<StructureNode> { n });

        (await sut.GetByIdAsync(n.Id)).Should().NotBeNull();
        (await sut.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetRootsAsync_MapsRoots()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetRootsAsync()).ReturnsAsync(new List<StructureNode> { Node(StructureNodeType.University, "/r", 0) });
        (await sut.GetRootsAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetChildrenAsync_MapsChildren()
    {
        var (sut, repo, _, _) = Build();
        var pid = Guid.NewGuid();
        repo.Setup(r => r.GetChildrenOnlyAsync(pid)).ReturnsAsync(new List<StructureNode> { Node(StructureNodeType.Faculty, "/r/f", 1) });
        (await sut.GetChildrenAsync(pid)).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBreadcrumbAsync_Missing_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);
        var act = () => sut.GetBreadcrumbAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<Exception>().WithMessage("Node not found");
    }

    [Fact]
    public async Task GetBreadcrumbAsync_OrdersByPathSequence()
    {
        var (sut, repo, _, _) = Build();
        var uni = Node(StructureNodeType.University, "", 0);
        var fac = Node(StructureNodeType.Faculty, "", 1);
        var node = Node(StructureNodeType.Program, $"/{uni.Id}/{fac.Id}", 2);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        // Returned out of order; service must reorder to match the path sequence.
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>())).ReturnsAsync(new List<StructureNode> { fac, uni });

        var crumbs = await sut.GetBreadcrumbAsync(node.Id);

        crumbs.Select(c => c.Id).Should().ContainInOrder(uni.Id, fac.Id);
    }

    [Fact]
    public async Task GetSubTreeAsync_Missing_ReturnsNull()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);
        (await sut.GetSubTreeAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetSubTreeAsync_BuildsNestedTreeFromRoot()
    {
        var (sut, repo, _, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/u/f", 1);
        var child = Node(StructureNodeType.Program, "/u/f/p", 2, parentId: node.Id);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetDescendantsTreeAsync("/u/f")).ReturnsAsync(new List<StructureNode> { node, child });

        var dto = await sut.GetSubTreeAsync(node.Id);

        dto!.Id.Should().Be(node.Id);
        dto.Children.Should().ContainSingle(c => c.Id == child.Id);
    }

    [Fact]
    public async Task GetAncestorsChainAsync_Missing_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);
        var act = () => sut.GetAncestorsChainAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<Exception>().WithMessage("Node not found");
    }

    [Fact]
    public async Task GetAncestorsChainAsync_ExcludesSelfAndOrdersByPath()
    {
        var (sut, repo, _, _) = Build();
        var uni = Node(StructureNodeType.University, "", 0);
        var fac = Node(StructureNodeType.Faculty, "", 1);
        var node = Node(StructureNodeType.Program, $"/{uni.Id}/{fac.Id}/", 2);
        // Patch the node's path to include itself last.
        node.Path = $"/{uni.Id}/{fac.Id}/{node.Id}";
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetAncestorsAsync(It.IsAny<List<Guid>>())).ReturnsAsync(new List<StructureNode> { fac, uni });

        var chain = await sut.GetAncestorsChainAsync(node.Id);

        chain.Should().HaveCount(2, "self is removed from the ancestor set");
        chain.Select(c => c.Id).Should().ContainInOrder(uni.Id, fac.Id);
        chain.Select(c => c.Id).Should().NotContain(node.Id);
    }

    // ── ReorderNodeAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ReorderNode_Missing_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);
        var act = () => sut.ReorderNodeAsync(Guid.NewGuid(), new ReorderNodeRequest());
        await act.Should().ThrowAsync<Exception>().WithMessage("Node not found");
    }

    [Fact]
    public async Task ReorderNode_InsertsAtTargetAndReindexesSequentially()
    {
        var (sut, repo, uow, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/u/f0", 1, order: 0);
        var s1 = Node(StructureNodeType.Faculty, "/u/f1", 1, order: 1);
        var s2 = Node(StructureNodeType.Faculty, "/u/f2", 1, order: 2);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetSiblingsAsync(node.ParentId)).ReturnsAsync(new List<StructureNode> { node, s1, s2 });
        List<StructureNode>? captured = null;
        repo.Setup(r => r.UpdateRangeAsync(It.IsAny<List<StructureNode>>())).Callback<List<StructureNode>>(l => captured = l);

        await sut.ReorderNodeAsync(node.Id, new ReorderNodeRequest { NewOrder = 1 });

        // Siblings after removing node: [s1, s2]; insert node at index 1 -> [s1, node, s2]
        captured.Should().NotBeNull();
        captured!.Select(x => x.Id).Should().ContainInOrder(s1.Id, node.Id, s2.Id);
        captured.Select(x => x.Order).Should().ContainInOrder(0, 1, 2);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ReorderNode_NegativeTarget_ClampsToFront()
    {
        var (sut, repo, _, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/u/f0", 1);
        var s1 = Node(StructureNodeType.Faculty, "/u/f1", 1);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetSiblingsAsync(node.ParentId)).ReturnsAsync(new List<StructureNode> { node, s1 });
        List<StructureNode>? captured = null;
        repo.Setup(r => r.UpdateRangeAsync(It.IsAny<List<StructureNode>>())).Callback<List<StructureNode>>(l => captured = l);

        await sut.ReorderNodeAsync(node.Id, new ReorderNodeRequest { NewOrder = -99 });

        captured![0].Id.Should().Be(node.Id, "negative target clamps to front");
    }

    [Fact]
    public async Task ReorderNode_TargetBeyondEnd_ClampsToBack()
    {
        var (sut, repo, _, _) = Build();
        var node = Node(StructureNodeType.Faculty, "/u/f0", 1);
        var s1 = Node(StructureNodeType.Faculty, "/u/f1", 1);
        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetSiblingsAsync(node.ParentId)).ReturnsAsync(new List<StructureNode> { node, s1 });
        List<StructureNode>? captured = null;
        repo.Setup(r => r.UpdateRangeAsync(It.IsAny<List<StructureNode>>())).Callback<List<StructureNode>>(l => captured = l);

        await sut.ReorderNodeAsync(node.Id, new ReorderNodeRequest { NewOrder = 999 });

        captured!.Last().Id.Should().Be(node.Id, "target beyond end clamps to back");
    }
}

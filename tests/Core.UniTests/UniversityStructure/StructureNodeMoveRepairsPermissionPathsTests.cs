using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using CapitalUniversity.Core.Application.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.UniversityStructure;

/// <summary>
/// Regression for the path-staleness bypass described in the audit:
/// <c>StaffRoleAssignment.StructureNodePath</c> snapshots are baked at write
/// time, so a structural reorganisation would otherwise silently keep users
/// scoped to the old subtree (or lock them out of the new one). The
/// <see cref="UniversityStructureService.MoveNodeAsync"/> wiring must call
/// <see cref="IStructureNodeRepository.RepairPermissionPathPrefixAsync"/>
/// with the right old/new path and then bump the global cache epoch so any
/// in-flight permission lookups are orphaned.
/// </summary>
public class StructureNodeMoveRepairsPermissionPathsTests
{
    [Fact]
    public async Task MoveNodeAsync_RewritesPermissionPathSnapshots_AndBumpsCacheEpoch()
    {
        var repo = new Mock<IStructureNodeRepository>(MockBehavior.Strict);
        var unitOfWork = new Mock<IUnitOfWork>();
        var cache = new Mock<IPermissionCacheInvalidator>();

        var faculty = new StructureNode
        {
            Id = Guid.NewGuid(),
            Type = StructureNodeType.Faculty,
            Name = "Old Faculty",
            Path = "/uni/old-faculty",
            Depth = 1,
            IsActive = true,
        };
        var newParent = new StructureNode
        {
            Id = Guid.NewGuid(),
            Type = StructureNodeType.University,
            Name = "Uni",
            Path = "/uni-new",
            Depth = 0,
            IsActive = true,
        };

        repo.Setup(r => r.GetByIdAsync(faculty.Id)).ReturnsAsync(faculty);
        repo.Setup(r => r.GetByIdAsync(newParent.Id)).ReturnsAsync(newParent);
        repo.Setup(r => r.GetDescendantsAsync(faculty.Path)).ReturnsAsync(new List<StructureNode> { faculty });
        repo.Setup(r => r.UpdateRangeAsync(It.IsAny<List<StructureNode>>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        string? capturedOldPath = null;
        string? capturedNewPath = null;
        repo.Setup(r => r.RepairPermissionPathPrefixAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((old, @new, _) =>
            {
                capturedOldPath = old;
                capturedNewPath = @new;
            })
            .ReturnsAsync(7);

        cache.Setup(c => c.InvalidateAllAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sut = new UniversityStructureService(repo.Object, new TestLocalizationService(), cache.Object);

        await sut.MoveNodeAsync(faculty.Id, new MoveStructureNodeRequest
        {
            NewParentId = newParent.Id,
            Order = 0,
        });

        capturedOldPath.Should().Be("/uni/old-faculty",
            "the original path must be passed verbatim so the prefix scan covers every descendant snapshot");
        capturedNewPath.Should().Be($"/uni-new/{faculty.Id}",
            "the rewritten prefix must match the new canonical path");

        cache.Verify(c => c.InvalidateAllAsync(It.IsAny<CancellationToken>()), Times.Once,
            "a structural move invalidates every cached permission lookup");
    }

    [Fact]
    public async Task MoveNodeAsync_NoOpMove_DoesNotTouchPermissionSnapshots()
    {
        // Guard: if MoveAsync is ever called with a no-op (same parent, same
        // order), the path doesn't change and we must not bump the global
        // cache epoch — bumping it would force every user in the system to
        // round-trip the DB for no reason.
        var repo = new Mock<IStructureNodeRepository>(MockBehavior.Strict);
        var unitOfWork = new Mock<IUnitOfWork>();
        var cache = new Mock<IPermissionCacheInvalidator>();

        var node = new StructureNode
        {
            Id = Guid.NewGuid(),
            Type = StructureNodeType.University,
            Name = "Uni",
            Path = string.Empty,
            Depth = 0,
            IsActive = true,
        };
        var samePath = $"/{node.Id}";
        node.Path = samePath;

        repo.Setup(r => r.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetDescendantsAsync(samePath)).ReturnsAsync(new List<StructureNode> { node });
        repo.Setup(r => r.UpdateRangeAsync(It.IsAny<List<StructureNode>>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var sut = new UniversityStructureService(repo.Object, new TestLocalizationService(), cache.Object);

        await sut.MoveNodeAsync(node.Id, new MoveStructureNodeRequest
        {
            NewParentId = null,
            Order = 0,
        });

        repo.Verify(
            r => r.RepairPermissionPathPrefixAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no path change ⇒ no snapshot rewrite");
        cache.Verify(
            c => c.InvalidateAllAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "no path change ⇒ no global cache epoch bump");
    }
}
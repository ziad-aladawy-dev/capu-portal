using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

/// <summary>
/// EffectiveScope renders row-level access decisions. Every branch is pinned:
/// the system-bypass short-circuit, unauthenticated denial, student
/// own-row/ancestor matching, global-scope short-circuit, path-prefix grant
/// matching, the row-not-found (null path) denial, and the header-lock checks
/// for year/semester — so conditional / boundary / string-comparison mutations
/// are killed. Backed by the EF in-memory provider for the two DB lookups.
/// </summary>
public sealed class EffectiveScopeTests : IDisposable
{
    private readonly CoreDbContext _db;

    public EffectiveScopeTests()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"effective-scope-{Guid.NewGuid():N}")
            .Options;
        _db = new CoreDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private (Guid StudentId, Guid NodeId) SeedStudent(string path)
    {
        var node = new StructureNode { Name = "n", Type = StructureNodeType.Level, Path = path };
        var student = new Student { StudentCode = "S1", StructureNodeId = node.Id, StructureNode = node };
        _db.StructureNodes.Add(node);
        _db.Students.Add(student);
        _db.SaveChanges();
        return (student.Id, node.Id);
    }

    private Guid SeedNode(string path)
    {
        var node = new StructureNode { Name = "n", Type = StructureNodeType.Faculty, Path = path };
        _db.StructureNodes.Add(node);
        _db.SaveChanges();
        return node.Id;
    }

    private EffectiveScope Build(
        Action<Mock<IUserScope>>? scope = null,
        bool isSystem = false,
        Guid? activeYear = null,
        Guid? activeSemester = null)
    {
        var userScope = new Mock<IUserScope>();
        userScope.Setup(s => s.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        userScope.SetupGet(s => s.UserId).Returns(Guid.NewGuid()); // authenticated by default
        userScope.SetupGet(s => s.AuthorizedNodePaths).Returns(new HashSet<string>());
        scope?.Invoke(userScope);

        var exec = new Mock<IExecutionContext>();
        exec.SetupGet(e => e.IsSystem).Returns(isSystem);

        var request = new Mock<IRequestContext>();
        request.SetupGet(r => r.ActiveAcademicYearId).Returns(activeYear);
        request.SetupGet(r => r.ActiveSemesterId).Returns(activeSemester);

        return new EffectiveScope(userScope.Object, _db, exec.Object, request.Object);
    }

    // ---------------- CanAccessStudentAsync ----------------

    [Fact]
    public async Task Student_SystemContext_AlwaysAllowed()
    {
        var sut = Build(isSystem: true);
        (await sut.CanAccessStudentAsync(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task Student_Unauthenticated_Denied()
    {
        var sut = Build(s => s.SetupGet(x => x.UserId).Returns(Guid.Empty));
        (await sut.CanAccessStudentAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Student_CallerIsStudent_SeesOwnRowOnly()
    {
        var me = Guid.NewGuid();
        var sut = Build(s =>
        {
            s.SetupGet(x => x.UserId).Returns(me);
            s.SetupGet(x => x.IsStudent).Returns(true);
        });

        (await sut.CanAccessStudentAsync(me)).Should().BeTrue();
        (await sut.CanAccessStudentAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Student_GlobalScope_Allowed()
    {
        var sut = Build(s => s.SetupGet(x => x.HasGlobalScope).Returns(true));
        (await sut.CanAccessStudentAsync(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task Student_NotFound_Denied()
    {
        var sut = Build(); // staff, no global scope, no grants
        (await sut.CanAccessStudentAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Student_PathCoveredByGrant_Allowed()
    {
        var (studentId, _) = SeedStudent("/Univ/Fac1/Prog/Lvl1");
        var sut = Build(s => s.SetupGet(x => x.AuthorizedNodePaths)
                              .Returns(new HashSet<string> { "/Univ/Fac1" }));

        (await sut.CanAccessStudentAsync(studentId)).Should().BeTrue();
    }

    [Fact]
    public async Task Student_PathOutsideGrant_Denied()
    {
        var (studentId, _) = SeedStudent("/Univ/Fac2/Prog");
        var sut = Build(s => s.SetupGet(x => x.AuthorizedNodePaths)
                              .Returns(new HashSet<string> { "/Univ/Fac1" }));

        (await sut.CanAccessStudentAsync(studentId)).Should().BeFalse();
    }

    // ---------------- CanAccessStructureNodeAsync ----------------

    [Fact]
    public async Task Node_SystemContext_AlwaysAllowed()
    {
        var sut = Build(isSystem: true);
        (await sut.CanAccessStructureNodeAsync(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task Node_Unauthenticated_Denied()
    {
        var sut = Build(s => s.SetupGet(x => x.UserId).Returns(Guid.Empty));
        (await sut.CanAccessStructureNodeAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Node_NotFound_Denied()
    {
        var sut = Build(s => s.SetupGet(x => x.HasGlobalScope).Returns(true));
        // Global scope, but the node row doesn't exist → null path short-circuits to denied.
        (await sut.CanAccessStructureNodeAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Node_StudentSeesAncestorOfOwnPath()
    {
        var nodeId = SeedNode("/Univ/Fac1");
        var sut = Build(s =>
        {
            s.SetupGet(x => x.IsStudent).Returns(true);
            s.SetupGet(x => x.OwnStructureNodePath).Returns("/Univ/Fac1/Prog/Lvl1");
        });

        (await sut.CanAccessStructureNodeAsync(nodeId)).Should().BeTrue();
    }

    [Fact]
    public async Task Node_StudentDeniedNonAncestor()
    {
        var nodeId = SeedNode("/Univ/Fac2");
        var sut = Build(s =>
        {
            s.SetupGet(x => x.IsStudent).Returns(true);
            s.SetupGet(x => x.OwnStructureNodePath).Returns("/Univ/Fac1/Prog");
        });

        (await sut.CanAccessStructureNodeAsync(nodeId)).Should().BeFalse();
    }

    [Fact]
    public async Task Node_StudentWithNoOwnPath_Denied()
    {
        var nodeId = SeedNode("/Univ/Fac1");
        var sut = Build(s =>
        {
            s.SetupGet(x => x.IsStudent).Returns(true);
            s.SetupGet(x => x.OwnStructureNodePath).Returns((string?)null);
        });

        (await sut.CanAccessStructureNodeAsync(nodeId)).Should().BeFalse();
    }

    [Fact]
    public async Task Node_StaffGlobalScope_Allowed()
    {
        var nodeId = SeedNode("/Univ/Fac1");
        var sut = Build(s => s.SetupGet(x => x.HasGlobalScope).Returns(true));

        (await sut.CanAccessStructureNodeAsync(nodeId)).Should().BeTrue();
    }

    [Fact]
    public async Task Node_StaffPathCoveredByGrant_Allowed()
    {
        var nodeId = SeedNode("/Univ/Fac1/Prog");
        var sut = Build(s => s.SetupGet(x => x.AuthorizedNodePaths)
                              .Returns(new HashSet<string> { "/Univ/Fac1" }));

        (await sut.CanAccessStructureNodeAsync(nodeId)).Should().BeTrue();
    }

    // ---------------- CanAccessAcademicYearAsync ----------------

    [Fact]
    public async Task Year_System_Allowed()
    {
        var sut = Build(isSystem: true, activeYear: Guid.NewGuid());
        (await sut.CanAccessAcademicYearAsync(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task Year_NoActiveLock_Allowed()
    {
        var sut = Build(activeYear: null);
        (await sut.CanAccessAcademicYearAsync(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task Year_MatchingLock_Allowed()
    {
        var id = Guid.NewGuid();
        var sut = Build(activeYear: id);
        (await sut.CanAccessAcademicYearAsync(id)).Should().BeTrue();
    }

    [Fact]
    public async Task Year_MismatchedLock_Denied()
    {
        var sut = Build(activeYear: Guid.NewGuid());
        (await sut.CanAccessAcademicYearAsync(Guid.NewGuid())).Should().BeFalse();
    }

    // ---------------- CanAccessSemesterAsync ----------------

    [Fact]
    public async Task Semester_System_Allowed()
    {
        var sut = Build(isSystem: true, activeSemester: Guid.NewGuid());
        (await sut.CanAccessSemesterAsync(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task Semester_NoActiveLock_Allowed()
    {
        var sut = Build(activeSemester: null);
        (await sut.CanAccessSemesterAsync(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task Semester_MatchingLock_Allowed()
    {
        var id = Guid.NewGuid();
        var sut = Build(activeSemester: id);
        (await sut.CanAccessSemesterAsync(id)).Should().BeTrue();
    }

    [Fact]
    public async Task Semester_MismatchedLock_Denied()
    {
        var sut = Build(activeSemester: Guid.NewGuid());
        (await sut.CanAccessSemesterAsync(Guid.NewGuid())).Should().BeFalse();
    }
}

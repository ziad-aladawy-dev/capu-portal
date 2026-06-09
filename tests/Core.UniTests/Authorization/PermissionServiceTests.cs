using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

// Disambiguate the authz entity from the CapitalUniversity.Module.* module
// namespace. Declared inside the namespace scope so the alias resolves before
// the outer `Module` namespace shadows the bare type name.
using Module = CapitalUniversity.Core.Domain.Authorization.Module;

/// <summary>
/// PermissionService is the raw permission-row reader. These tests pin the
/// resource-key narrowing, the override stream, and — most importantly — the
/// inline scope predicate in <c>LoadAssignmentsAsync</c>/<c>LoadOverridesAsync</c>:
/// Global vs. exact year/semester matching and the StructureNodePath prefix
/// rule. Backed by EF in-memory so the LINQ predicate executes as written,
/// killing conditional / boundary / string-comparison mutations.
/// </summary>
public sealed class PermissionServiceTests : IDisposable
{
    private readonly CoreDbContext _db;
    private readonly PermissionService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public PermissionServiceTests()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"perm-svc-{Guid.NewGuid():N}")
            .Options;
        _db = new CoreDbContext(options);
        _db.Database.EnsureCreated();
        _sut = new PermissionService(_db);
    }

    public void Dispose() => _db.Dispose();

    private (Guid RoleId, Guid ResourceId) SeedResourceWithRole(string resourceKey)
    {
        var module = new Module { Id = Guid.NewGuid(), DisplayName = "M", ModuleKey = $"mod-{resourceKey}" };
        var resource = new Resource { Id = Guid.NewGuid(), Module = module, ModuleId = module.Id, Key = resourceKey, DisplayName = "R" };
        var role = new Role { Id = Guid.NewGuid(), Name = $"role-{resourceKey}" };
        _db.Modules.Add(module);
        _db.Resources.Add(resource);
        _db.Roles.Add(role);
        _db.AddCrudGrant(role.Id, resource.Id, "View");
        return (role.Id, resource.Id);
    }

    private void AssignRole(Guid roleId, string year = "Global", string semester = "Global", string? path = null)
    {
        var a = new StaffRoleAssignment(_userId, roleId, year, semester) { StructureNodePath = path };
        _db.StaffRoles.Add(a);
        _db.SaveChanges();
    }

    private static AuthorizationScope Scope(string year = "Global", string semester = "Global", string? path = null) =>
        new() { Year = year, Semester = semester, StructureNodePath = path };

    // ---------------- basic load ----------------

    [Fact]
    public async Task GetAll_ReturnsAssignmentsAndRolePermissions()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId);

        var result = await _sut.GetAllPermissionsAsync(_userId);

        result.Assignments.Should().ContainSingle();
        result.RolePermissions.Should().ContainSingle(); // View only
        result.Overrides.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_NoAssignments_ReturnsEmptyStreams()
    {
        var result = await _sut.GetAllPermissionsAsync(_userId);
        result.Assignments.Should().BeEmpty();
        result.RolePermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_IncludesOverrides()
    {
        var (roleId, resourceId) = SeedResourceWithRole("demo");
        AssignRole(roleId);
        _db.StaffPermissions.Add(new StaffPermissionOverride(_userId, resourceId, "View", OverrideType.Deny, "Global", "Global"));
        _db.SaveChanges();

        var result = await _sut.GetAllPermissionsAsync(_userId);
        result.Overrides.Should().ContainSingle();
    }

    // ---------------- resource-key narrowing ----------------

    [Fact]
    public async Task GetResource_FiltersRolePermissionsByKey()
    {
        var (roleA, _) = SeedResourceWithRole("alpha");
        var (roleB, _) = SeedResourceWithRole("beta");
        AssignRole(roleA);
        AssignRole(roleB);

        var result = await _sut.GetResourcePermissionsAsync(_userId, "alpha");

        result.RolePermissions.Should().ContainSingle();
        result.RolePermissions.Cast<IRolePermission>()
            .Should().OnlyContain(rp => rp.ResourceId == result.RolePermissions.First().ResourceId);
        // Both role assignments still load (assignments are not resource-filtered).
        result.Assignments.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetResource_FiltersOverridesByKey()
    {
        var (roleA, resA) = SeedResourceWithRole("alpha");
        var (_, resB) = SeedResourceWithRole("beta");
        AssignRole(roleA);
        _db.StaffPermissions.Add(new StaffPermissionOverride(_userId, resA, "View", OverrideType.Allow, "Global", "Global"));
        _db.StaffPermissions.Add(new StaffPermissionOverride(_userId, resB, "View", OverrideType.Allow, "Global", "Global"));
        _db.SaveChanges();

        var result = await _sut.GetResourcePermissionsAsync(_userId, "alpha");
        result.Overrides.Should().ContainSingle();
    }

    // ---------------- scope predicate: year/semester ----------------

    [Fact]
    public async Task Scope_Null_NoFiltering_ReturnsNonGlobalAssignment()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId, year: "2025", semester: "Fall");

        var result = await _sut.GetAllPermissionsAsync(_userId, scope: null);
        result.Assignments.Should().ContainSingle();
    }

    [Fact]
    public async Task Scope_GlobalAssignment_MatchesAnyScope()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId, year: "Global", semester: "Global");

        var result = await _sut.GetAllPermissionsAsync(_userId, Scope(year: "2025", semester: "Fall"));
        result.Assignments.Should().ContainSingle();
    }

    [Fact]
    public async Task Scope_ExactYearMatch_Included()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId, year: "2025", semester: "Global");

        var result = await _sut.GetAllPermissionsAsync(_userId, Scope(year: "2025", semester: "Fall"));
        result.Assignments.Should().ContainSingle();
    }

    [Fact]
    public async Task Scope_YearMismatch_Excluded()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId, year: "2024", semester: "Global");

        var result = await _sut.GetAllPermissionsAsync(_userId, Scope(year: "2025", semester: "Fall"));
        result.Assignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Scope_SemesterMismatch_Excluded()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId, year: "Global", semester: "Spring");

        var result = await _sut.GetAllPermissionsAsync(_userId, Scope(year: "2025", semester: "Fall"));
        result.Assignments.Should().BeEmpty();
    }

    // ---------------- scope predicate: structure-node path prefix ----------------

    [Fact]
    public async Task Scope_NullGrantPath_AlwaysIncluded()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId, path: null);

        var result = await _sut.GetAllPermissionsAsync(_userId, Scope(path: "/Univ/Fac1"));
        result.Assignments.Should().ContainSingle();
    }

    [Fact]
    public async Task Scope_GrantPathIsPrefixOfActive_Included()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId, path: "/Univ/Fac1");

        var result = await _sut.GetAllPermissionsAsync(_userId, Scope(path: "/Univ/Fac1/Prog"));
        result.Assignments.Should().ContainSingle();
    }

    [Fact]
    public async Task Scope_GrantPathNotPrefix_Excluded()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId, path: "/Univ/Fac2");

        var result = await _sut.GetAllPermissionsAsync(_userId, Scope(path: "/Univ/Fac1/Prog"));
        result.Assignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Scope_GrantPathSetButActivePathNull_Excluded()
    {
        var (roleId, _) = SeedResourceWithRole("demo");
        AssignRole(roleId, path: "/Univ/Fac1");

        var result = await _sut.GetAllPermissionsAsync(_userId, Scope(path: null));
        result.Assignments.Should().BeEmpty();
    }
}

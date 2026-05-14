using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.Audit;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.CrossCutting.Security;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class AuthorizationEvaluatorTests
{
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly AuthorizationEvaluator _evaluator;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _resource = "Student";
    private readonly AuthorizationScope _scope = new() { Domain = "F1", UniversityId = null, FacultyId = Guid.Parse("00000000-0000-0000-0000-000000000001"), ProgramId = null, Year = "Y1", Semester = "S1" };

    public AuthorizationEvaluatorTests()
    {
        _loggerMock = new Mock<ILoggerService>();
        _evaluator = new AuthorizationEvaluator(_loggerMock.Object);
    }

    // --- Deny Overrides ---

    [Fact]
    public void Evaluate_WithMatchingDenyOverride_ShouldReturnDeny()
    {
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.View, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1", OverrideType.Deny)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, _scope, overrides, [], []);

        result.IsAllowed.Should().BeFalse();
        result.SourceType.Should().Be(SourceType.None);
    }

    [Fact]
    public void Evaluate_WithMatchingDenyOverrideWildcard_ShouldReturnDeny()
    {
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(Guid.NewGuid(), "*", ActionLevel.View, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1", OverrideType.Deny)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, _scope, overrides, [], []);

        result.IsAllowed.Should().BeFalse();
        result.SourceType.Should().Be(SourceType.None);
    }

    [Fact]
    public void Evaluate_WithDenyOverrideOnDifferentScope_ShouldNotReturnDeny()
    {
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.View, null, Guid.Parse("00000000-0000-0000-0000-000000000002"), null, "Y1", "S1", OverrideType.Deny)
        };

        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(Guid.NewGuid(), null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleAssignments[0].RoleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, _scope, overrides, roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
    }

    // --- Allow Overrides ---

    [Fact]
    public void Evaluate_WithMatchingAllowOverride_ShouldReturnAllow()
    {
        var overrideId = Guid.NewGuid();
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(overrideId, _resource, ActionLevel.View, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1", OverrideType.Allow)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, _scope, overrides, [], []);

        result.IsAllowed.Should().BeTrue();
        result.SourceType.Should().Be(SourceType.UserOverride);
        result.SourceId.Should().Be(overrideId);
    }

    [Fact]
    public void Evaluate_WithAllowOverrideAndHigherRolePermission_ShouldReturnAllowFromRole()
    {
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.View, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1", OverrideType.Allow)
        };

        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.EditClose)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.EditClose, false, _scope, overrides, roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
        result.SourceType.Should().Be(SourceType.RoleAssignment);
        result.SourceId.Should().Be(roleId);
    }

    [Fact]
    public void Evaluate_WithAllowOverrideHigherThanRolePermission_ShouldReturnAllowFromOverride()
    {
        var overrideId = Guid.NewGuid();
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(overrideId, _resource, ActionLevel.EditClose, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1", OverrideType.Allow)
        };

        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.EditClose, false, _scope, overrides, roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
        result.SourceType.Should().Be(SourceType.UserOverride);
        result.SourceId.Should().Be(overrideId);
    }

    // --- Role Permissions ---

    [Fact]
    public void Evaluate_WithMatchingRoleAssignment_ShouldReturnAllow()
    {
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, _scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
        result.SourceType.Should().Be(SourceType.RoleAssignment);
        result.SourceId.Should().Be(roleId);
    }

    [Fact]
    public void Evaluate_WithMatchingRoleAssignmentWildcard_ShouldReturnAllow()
    {
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, "*", ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, _scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
        result.SourceType.Should().Be(SourceType.RoleAssignment);
        result.SourceId.Should().Be(roleId);
    }

    [Fact]
    public void Evaluate_WithRoleAssignmentOnDifferentScope_ShouldReturnDeny()
    {
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000002"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, _scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeFalse();
        result.SourceType.Should().Be(SourceType.None);
    }

    // --- ABAC Closed Records ---

    [Fact]
    public void Evaluate_ClosedRecordInsert_RequiresOpenLevel()
    {
        var overrideId = Guid.NewGuid();
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(overrideId, _resource, ActionLevel.Insert, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1", OverrideType.Allow)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.Insert, true, _scope, overrides, [], []);

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ClosedRecordInsert_WithOpenLevel_ShouldReturnAllow()
    {
        var overrideId = Guid.NewGuid();
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(overrideId, _resource, ActionLevel.Open, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1", OverrideType.Allow)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.Insert, true, _scope, overrides, [], []);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ClosedRecordEditClose_RequiresOpenLevel()
    {
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.EditClose)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.EditClose, true, _scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ClosedRecordEditClose_WithOpenLevel_ShouldReturnAllow()
    {
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.Open)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.EditClose, true, _scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ClosedRecordDelete_WithOpenLevel_ShouldReturnDeny()
    {
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.Open)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.Delete, true, _scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ClosedRecordDelete_WithDeleteLevel_ShouldReturnAllow()
    {
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.Delete)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.Delete, true, _scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
    }
}

internal class TestPermissionOverride : IUserPermissionOverride
{
    public Guid Id { get; }
    public string Resource { get; }
    public ActionLevel Level { get; }
    public Guid? UniversityId { get; }
    public Guid? FacultyId { get; }
    public Guid? ProgramId { get; }
    public string Year { get; }
    public string Semester { get; }
    public OverrideType Type { get; }

    public TestPermissionOverride(Guid id, string resource, ActionLevel level, Guid? universityId, Guid? facultyId, Guid? programId, string year, string semester, OverrideType type)
    {
        Id = id;
        Resource = resource;
        Level = level;
        UniversityId = universityId;
        FacultyId = facultyId;
        ProgramId = programId;
        Year = year;
        Semester = semester;
        Type = type;
    }
}

internal class TestRoleAssignment : IUserRoleAssignment
{
    public Guid RoleId { get; }
    public Guid? UniversityId { get; }
    public Guid? FacultyId { get; }
    public Guid? ProgramId { get; }
    public string Year { get; }
    public string Semester { get; }

    public TestRoleAssignment(Guid roleId, Guid? universityId, Guid? facultyId, Guid? programId, string year, string semester)
    {
        RoleId = roleId;
        UniversityId = universityId;
        FacultyId = facultyId;
        ProgramId = programId;
        Year = year;
        Semester = semester;
    }
}

internal class TestRolePermission : IRolePermission
{
    public Guid RoleId { get; }
    public string Resource { get; }
    public ActionLevel Level { get; }

    public TestRolePermission(Guid roleId, string resource, ActionLevel level)
    {
        RoleId = roleId;
        Resource = resource;
        Level = level;
    }
}


using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.CrossCutting.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Abstractions.Shared;
using FluentAssertions;
using Moq;
using Xunit;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Authorization;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class AuthorizationEvaluatorTests
{
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly AuthorizationEvaluator _evaluator;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _resource = "Student";
    private readonly AuthorizationScope _scope = new() { Domain = "F1", StructureNodeId = Guid.Parse("00000000-0000-0000-0000-000000000001"), StructureNodePath = "/1/2", Year = "Y1", Semester = "S1" };

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
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.View, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1", OverrideType.Deny)
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
            new TestPermissionOverride(Guid.NewGuid(), "*", ActionLevel.View, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1", OverrideType.Deny)
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
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.View, Guid.Parse("00000000-0000-0000-0000-000000000002"), "/1/4", "Y1", "S1", OverrideType.Deny)
        };

        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(Guid.NewGuid(), Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleAssignments[0].RoleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, _scope, overrides, roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithDenyOverrideOnChildPath_ShouldNotReturnDeny()
    {
        // Scope is at University (/1)
        var scope = new AuthorizationScope { Domain = "F1", StructureNodeId = Guid.NewGuid(), StructureNodePath = "/1", Year = "Y1", Semester = "S1" };
        
        // Deny is at Faculty (/1/2)
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.View, Guid.NewGuid(), "/1/2", "Y1", "S1", OverrideType.Deny)
        };

        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, Guid.NewGuid(), "/1", "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, scope, overrides, roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue("because child path deny should not affect parent access.");
    }

    [Fact]
    public void Evaluate_WithDenyOverrideOnParentPath_ShouldReturnDeny()
    {
        // Scope is at Faculty (/1/2)
        var scope = new AuthorizationScope { Domain = "F1", StructureNodeId = Guid.NewGuid(), StructureNodePath = "/1/2", Year = "Y1", Semester = "S1" };
        
        // Deny is at University (/1)
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.View, Guid.NewGuid(), "/1", "Y1", "S1", OverrideType.Deny)
        };

        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, Guid.NewGuid(), "/1/2", "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, scope, overrides, roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeFalse("because parent path deny should cascade down.");
    }


    // --- Allow Overrides ---

    [Fact]
    public void Evaluate_WithMatchingAllowOverride_ShouldReturnAllow()
    {
        var overrideId = Guid.NewGuid();
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(overrideId, _resource, ActionLevel.View, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1", OverrideType.Allow)
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
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.View, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1", OverrideType.Allow)
        };

        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1")
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
            new TestPermissionOverride(overrideId, _resource, ActionLevel.EditClose, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1", OverrideType.Allow)
        };

        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1")
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
            new TestRoleAssignment(roleId, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1")
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
            new TestRoleAssignment(roleId, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1")
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
            new TestRoleAssignment(roleId, Guid.Parse("00000000-0000-0000-0000-000000000002"), "/1/4", "Y1", "S1")
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
            new TestPermissionOverride(overrideId, _resource, ActionLevel.Insert, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1", OverrideType.Allow)
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
            new TestPermissionOverride(overrideId, _resource, ActionLevel.Open, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1", OverrideType.Allow)
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
            new TestRoleAssignment(roleId, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1")
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
            new TestRoleAssignment(roleId, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1")
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
            new TestRoleAssignment(roleId, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1")
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
            new TestRoleAssignment(roleId, Guid.Parse("00000000-0000-0000-0000-000000000001"), "/1/2", "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.Delete)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.Delete, true, _scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
    }

    // --- Hierarchical Logic ---

    [Fact]
    public void Evaluate_ParentPermission_ShouldGrantChildAccess()
    {
        // Scope is at Program level (/1/2/3)
        var scope = new AuthorizationScope { Domain = "F1", StructureNodeId = Guid.NewGuid(), StructureNodePath = "/1/2/3", Year = "Y1", Semester = "S1" };
        
        // Permission is at University level (/1)
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, Guid.NewGuid(), "/1", "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue("because University level permission should grant access to all its children (Faculties and Programs).");
    }

    [Fact]
    public void Evaluate_ChildPermission_ShouldNotGrantParentAccess()
    {
        // Scope is at University level (/1)
        var scope = new AuthorizationScope { Domain = "F1", StructureNodeId = Guid.NewGuid(), StructureNodePath = "/1", Year = "Y1", Semester = "S1" };
        
        // Permission is at Faculty level (/1/2)
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, Guid.NewGuid(), "/1/2", "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeFalse("because Faculty level permission should not grant access to the parent University.");
    }

    [Fact]
    public void Evaluate_SiblingPermission_ShouldNotGrantAccess()
    {
        // Scope is at Faculty 2 level (/1/3)
        var scope = new AuthorizationScope { Domain = "F1", StructureNodeId = Guid.NewGuid(), StructureNodePath = "/1/3", Year = "Y1", Semester = "S1" };
        
        // Permission is at Faculty 1 level (/1/2)
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment>
        {
            new TestRoleAssignment(roleId, Guid.NewGuid(), "/1/2", "Y1", "S1")
        };
        var rolePermissions = new List<IRolePermission>
        {
            new TestRolePermission(roleId, _resource, ActionLevel.View)
        };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeFalse("because permission on one branch should not grant access to a sibling branch.");
    }

    [Fact]
    public void Evaluate_WithEmptyScopePath_ShouldReturnDeny()
    {
        var scope = new AuthorizationScope { Domain = "F1", StructureNodeId = Guid.NewGuid(), StructureNodePath = "", Year = "Y1", Semester = "S1" };
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment> { new TestRoleAssignment(roleId, Guid.NewGuid(), "/1", "Y1", "S1") };
        var rolePermissions = new List<IRolePermission> { new TestRolePermission(roleId, _resource, ActionLevel.View) };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeFalse("because empty scope path should not match anything.");
    }

    [Fact]
    public void Evaluate_WithPartialPathMatch_ShouldReturnDeny()
    {
        // Scope path /1/11 should NOT match /1/1
        var scope = new AuthorizationScope { Domain = "F1", StructureNodeId = Guid.NewGuid(), StructureNodePath = "/1/11", Year = "Y1", Semester = "S1" };
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment> { new TestRoleAssignment(roleId, Guid.NewGuid(), "/1/1", "Y1", "S1") };
        var rolePermissions = new List<IRolePermission> { new TestRolePermission(roleId, _resource, ActionLevel.View) };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeFalse("because /1/11 is not a child of /1/1.");
    }

    [Fact]
    public void Evaluate_WithTrailingSlashPathMatch_ShouldReturnAllow()
    {
        // Scope path /1/1 matches assignment path /1/1
        var scope = new AuthorizationScope { Domain = "F1", StructureNodeId = Guid.NewGuid(), StructureNodePath = "/1/1", Year = "Y1", Semester = "S1" };
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment> { new TestRoleAssignment(roleId, Guid.NewGuid(), "/1/1", "Y1", "S1") };
        var rolePermissions = new List<IRolePermission> { new TestRolePermission(roleId, _resource, ActionLevel.View) };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue();
    }
}

internal class TestPermissionOverride : IUserPermissionOverride
{
    public Guid Id { get; }
    public string Resource { get; }
    public ActionLevel Level { get; }
    public Guid? StructureNodeId { get; }
    public string? StructureNodePath { get; }
    public string Year { get; }
    public string Semester { get; }
    public OverrideType Type { get; }

    public TestPermissionOverride(Guid id, string resource, ActionLevel level, Guid? structureNodeId, string? structureNodePath, string year, string semester, OverrideType type)
    {
        Id = id;
        Resource = resource;
        Level = level;
        StructureNodeId = structureNodeId;
        StructureNodePath = structureNodePath;
        Year = year;
        Semester = semester;
        Type = type;
    }
}

internal class TestRoleAssignment : IUserRoleAssignment
{
    public Guid RoleId { get; }
    public Guid? StructureNodeId { get; }
    public string? StructureNodePath { get; }
    public string Year { get; }
    public string Semester { get; }

    public TestRoleAssignment(Guid roleId, Guid? structureNodeId, string? structureNodePath, string year, string semester)
    {
        RoleId = roleId;
        StructureNodeId = structureNodeId;
        StructureNodePath = structureNodePath;
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

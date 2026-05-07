using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.Audit;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.CrossCutting.Security;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class AuthorizationEvaluatorActionLevelTests
{
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly AuthorizationEvaluator _evaluator;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _resource = "Student";
    private readonly AuthorizationScope _scope = new() { Domain = "F1", Year = "Y1", Semester = "S1" };

    public AuthorizationEvaluatorActionLevelTests()
    {
        _loggerMock = new Mock<ILoggerService>();
        _evaluator = new AuthorizationEvaluator(_loggerMock.Object);
    }

    [Fact]
    public void Evaluate_DenyView_ShouldAlsoDenyEditClose()
    {
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.View, "F1", "Y1", "S1", OverrideType.Deny)
        };

        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment> { new TestRoleAssignment(roleId, "F1", "Y1", "S1") };
        var rolePermissions = new List<IRolePermission> { new TestRolePermission(roleId, _resource, ActionLevel.EditClose) };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.EditClose, false, _scope, overrides, roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeFalse("because denying a lower level (View) should implicitly deny all higher levels (EditClose) since permissions are hierarchical.");
    }

    [Fact]
    public void Evaluate_DenyEditClose_ShouldNotDenyView()
    {
        var overrides = new List<IUserPermissionOverride>
        {
            new TestPermissionOverride(Guid.NewGuid(), _resource, ActionLevel.EditClose, "F1", "Y1", "S1", OverrideType.Deny)
        };

        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment> { new TestRoleAssignment(roleId, "F1", "Y1", "S1") };
        var rolePermissions = new List<IRolePermission> { new TestRolePermission(roleId, _resource, ActionLevel.View) };

        var result = _evaluator.Evaluate(_userId, _resource, ActionLevel.View, false, _scope, overrides, roleAssignments, rolePermissions);

        result.IsAllowed.Should().BeTrue("because denying a higher level (EditClose) should not affect lower levels (View).");
    }
}

using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Abstractions.Audit;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.CrossCutting.Security;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class AuthorizationEvaluatorClosedTests
{
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly AuthorizationEvaluator _evaluator;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _resource = "Student";
    private readonly AuthorizationScope _scope = new() { Domain = "F1", UniversityId = null, FacultyId = Guid.Parse("00000000-0000-0000-0000-000000000001"), ProgramId = null, Year = "Y1", Semester = "S1" };

    public AuthorizationEvaluatorClosedTests()
    {
        _loggerMock = new Mock<ILoggerService>();
        _evaluator = new AuthorizationEvaluator(_loggerMock.Object);
    }

    [Theory]
    [InlineData(ActionLevel.View, ActionLevel.View, true)] // View is allowed on closed records if granted
    [InlineData(ActionLevel.Insert, ActionLevel.Insert, false)] // Insert requires Open
    [InlineData(ActionLevel.Insert, ActionLevel.Open, true)] // Insert granted via Open
    [InlineData(ActionLevel.EditClose, ActionLevel.EditClose, false)] // Edit requires Open
    [InlineData(ActionLevel.EditClose, ActionLevel.Open, true)] // Edit granted via Open
    [InlineData(ActionLevel.Delete, ActionLevel.Open, false)] // Delete needs Delete, Open is not enough
    [InlineData(ActionLevel.Delete, ActionLevel.Delete, true)] // Delete granted via Delete
    public void Evaluate_ClosedRecordConstraints_WorksCorrectly(ActionLevel requiredLevel, ActionLevel grantedLevel, bool expectedAllowed)
    {
        var roleId = Guid.NewGuid();
        var roleAssignments = new List<IUserRoleAssignment> { new TestRoleAssignment(roleId, null, Guid.Parse("00000000-0000-0000-0000-000000000001"), null, "Y1", "S1") };
        var rolePermissions = new List<IRolePermission> { new TestRolePermission(roleId, _resource, grantedLevel) };

        var result = _evaluator.Evaluate(_userId, _resource, requiredLevel, true, _scope, [], roleAssignments, rolePermissions);

        result.IsAllowed.Should().Be(expectedAllowed);
    }
}

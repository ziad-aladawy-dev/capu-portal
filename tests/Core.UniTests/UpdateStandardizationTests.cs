using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Application.Courses;
using CapitalUniversity.Core.Application.Courses.Mappings;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Mappings;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests;

public class UpdateStandardizationTests
{
    [Fact]
    public void CourseMapper_ApplyUpdate_ShouldBeSparse()
    {
        // Arrange
        var mapper = new CourseMapper();
        var entity = new Course
        {
            Id = Guid.NewGuid(),
            Code = "CS101",
            Title = "Original Title",
            CreditHours = 3,
            Category = CourseCategory.ProgramRequirement,
            IsActive = true
        };

        var request = new UpdateCourseRequest
        {
            Title = "Updated Title",
            // CreditHours, Category, IsActive are null/omitted
        };

        // Act
        mapper.ApplyUpdate(request, entity);

        // Assert
        // Mapperly uses LocalizedJson.Normalize, so we expect the JSON shape.
        entity.Title.Should().Be("{\"ar\":\"Updated Title\",\"en\":\"Updated Title\"}");
        entity.Code.Should().Be("CS101"); // Unchanged
        entity.CreditHours.Should().Be(3); // Unchanged
        entity.Category.Should().Be(CourseCategory.ProgramRequirement); // Unchanged
        entity.IsActive.Should().BeTrue(); // Unchanged
    }

    [Fact]
    public void AcademicPlanMapper_ApplyUpdate_ShouldBeSparse()
    {
        // Arrange
        var mapper = new AcademicPlanMapper();
        var entity = new AcademicPlan
        {
            Id = Guid.NewGuid(),
            Name = "Original Plan",
            StructureNodeId = Guid.NewGuid(),
            EffectiveFrom = DateTime.UtcNow.AddDays(-10),
            IsActive = true
        };

        var request = new UpdateAcademicPlanRequest
        {
            IsActive = false
            // Name, EffectiveFrom, EffectiveTo are null
        };

        // Act
        mapper.ApplyUpdate(request, entity);

        // Assert
        entity.IsActive.Should().BeFalse();
        // Since request.Name is null, the mapper skips it, preserving the original string.
        entity.Name.Should().Be("Original Plan");
    }

    [Fact]
    public void RoleMapper_ApplyUpdate_ShouldBeSparse()
    {
        // Arrange
        var mapper = new RoleMapper();
        var entity = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Original Role",
            IsSystemRole = true
        };

        var request = new UpdateRoleRequest
        {
            Id = entity.Id,
            Name = "Updated Role"
        };

        // Act
        mapper.ApplyUpdate(request, entity);

        // Assert
        entity.Name.Should().Be("{\"ar\":\"Updated Role\",\"en\":\"Updated Role\"}");
        entity.IsSystemRole.Should().BeTrue(); // Unchanged (not in DTO)
    }

    [Fact]
    public async Task UpdateRoleCommandHandler_ShouldPerformTrackedSparseUpdate()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new CoreDbContext(options);
        
        var role = new Role { Id = Guid.NewGuid(), Name = "Original", IsSystemRole = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var handler = new UpdateRoleCommandHandler(db, new TestLocalizationService());
        var request = new UpdateRoleRequest { Id = role.Id, Name = "Updated" };

        // Act
        var response = await handler.Handle(request, CancellationToken.None);

        // Assert
        response.Should().NotBeNull();
        response!.Name.Should().Be("Updated"); // Handler response should be decoded

        var updatedRole = await db.Roles.FindAsync(role.Id);
        updatedRole!.Name.Should().Be("{\"ar\":\"Updated\",\"en\":\"Updated\"}"); // Entity stores JSON
        updatedRole.IsSystemRole.Should().BeTrue(); // Unchanged
    }
}
